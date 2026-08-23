# Pre-outcome amendment: capacity là nhân tố thiết kế — WP8-011d

> Thời điểm: 2026-08-22, trước khi chạy bất kỳ job Layer-2/WP9 confirmatory nào.
> Chưa có confirmatory outcome khi amendment này được lập. Amendment này được lập
> cùng đợt với ADR-045 và **sau** ADR-045 về mặt phụ thuộc: không job confirmatory
> nào chạy được trước khi ngữ nghĩa `MAX_RIDE_TIME` được chốt.

## Phát hiện

Pilot frontier 25/25 (`wp8-008`) chứa một quét đội xe 4/8/16, hiện chỉ tồn tại ở
mức budget unbounded:

| Xe | B1 | C1 unbounded | Giá lock (pp, mẫu số 128) |
|---:|---:|---:|---:|
| 4 | 80 | 77 | −2,34 |
| 8 | 117 | 117 | 0,00 |
| 16 | 128 | 128 | 0,00 |

Ở 16 xe, B1 phục vụ 128/128 — đội xe bão hoà, phép đo mất khả năng phân biệt.
Ở 8 xe giá lock bằng 0 nhưng giá budget là −4,69 pp. Ở 4 xe giá lock đã là thật.

Nghĩa là chi phí dịch vụ của cơ chế cam kết **là hàm của mức sử dụng năng lực**,
không phải một hằng số. Một panel chỉ có 8 xe cho kết luận chỉ đúng ở 8 xe, và
không phân biệt được "cam kết rẻ" với "đội xe đủ dư để mọi cơ chế đều rẻ".

## Đội xe nằm trong derivative, không nằm trong execution plan

Kiểm trực tiếp trước khi chốt: `scenario-content.json` của mỗi cell chứa mảng
`fleet` và `validationSummary.vehicleCount = 8`, còn `grid-v2.json` khai
`vehicleCount` cho normalizer. Nghĩa là đội xe là thuộc tính của **derivative đã
đóng băng**, không phải tham số runtime. Đổi cỡ đội xe **bắt buộc** phải
materialize một derivative tree mới và sinh `scenarioHash` mới.

Hệ quả trực tiếp: đơn vị thí nghiệm đã khóa ở `RB-WP8-004` là
`(scenarioHash, demandRealizationHash, travelRealizationHash)`. Hai cỡ đội xe
khác `scenarioHash`, nên chúng **không** phải hai stratum trong cùng một đơn vị.
Coi chúng là stratum trong-đơn-vị sẽ phải định nghĩa lại `ExperimentalUnitIdentity`
sau khi thiết kế đã đóng băng, và điều đó không được phép.

## Sửa thiết kế

Vì vậy `veh4` được khai là **panel thứ hai, tách bạch**, không phải stratum trong
panel cũ:

1. Panel A giữ nguyên đúng 20 cell holdout đã đóng băng ở `veh8`
   (5 ngày × 4 demand realization, 108 arrivals/cell). Không đổi một byte nào.
2. Panel B là 20 cell **mới** materialize từ đúng cùng
   `(demandMemberPath, travelFactorMemberPath, window, requestTarget,
   selectionLabel, pseudonymizationLabel)`, chỉ khác `vehicleCount = 4` và
   `scenarioId`. Trước khi Panel B được dùng, phải chứng minh bằng đo đạc rằng
   tập 108 request được chọn của mỗi cell là **giống hệt** Panel A; nếu selection
   lệch thì hai panel không chia sẻ demand realization và amendment này fail closed.
3. **Không** thêm `veh16`: ở mức đó B1 đã bão hoà 128/128 trong pilot nên panel ấy
   không có khả năng phân giải, và đưa vào chỉ làm loãng báo cáo.

## Điều kiện "cùng demand realization" — đã đo, PASS

Panel B đã được materialize (`grid-v3-veh4.json`, derivative tree
`wp9-confirmatory-fixed-panel-v3-veh4`, 20/20 cell, 108 request/cell). Kiểm tra
điều kiện ở mục 2 bằng cách đọc `selection-frame.json` và `scenario-content.json`
của cả hai panel:

- **20/20 cell** có tập `sourceRecordOrdinal` được chọn **giống hệt** nhau;
- **20/20 cell** có tập `(originNodeId, destinationNodeId, arrivalTimeMs)` của 108
  request **giống hệt** nhau.

Điều này đúng theo cơ chế chứ không phải may mắn: rank chọn request là
`HMAC(selectionKey, "RideBound.Wp6.SourceSelection.v1", sourceArtifactSha256,
demandMemberPath, ordinal)` và node pool dùng
`NodePoolSeedPair.v1`/`NodePoolTieBreak.v1` — **không** cái nào đọc `vehicleCount`
hay `scenarioId`. Hai panel vì thế chia sẻ đúng một demand realization và đúng một
travel realization.

**Giới hạn phải ghi:** vị trí đội xe thì *khác*. Rank đặt xe là
`HMAC(selectionKey, "RideBound.Wp6.FleetPlacement.v1", sourceArtifactSha256,
scenarioId, node)` và `scenarioId` của Panel B khác Panel A, nên 4 xe của Panel B
**không** phải tập con của 8 xe Panel A — chúng là một rút thăm khác trên cùng node
pool. Vì vậy capacity contrast trộn hai thứ: số xe và vị trí xe. Đây là lý do nữa
để nó chỉ là diagnostic mô tả, không phải test.
4. Estimand:
   - **Primary** không đổi: `Δ_service_panel` và primary burden trên Panel A
     (`veh8`), margin strict `−1,0 pp`. Amendment này không nới, không đổi,
     không thêm điều kiện nào cho gate đã prereg.
   - **Co-primary có điều kiện**: cùng hai đại lượng trên Panel B (`veh4`), báo
     cáo riêng với chính margin `−1,0 pp`. Nó không cứu và không làm hỏng gate
     Panel A; hai panel được kết luận độc lập.
   - **Capacity contrast** là diagnostic mô tả: hiệu của hai delta giữa hai
     panel, không có gate, không p-value. Nó chỉ diễn giải được nếu điều kiện
     "cùng tập request" ở mục 2 đã PASS.
5. Luận điểm được phép phát biểu sau WP9 là luận điểm **có điều kiện**: chi phí
   dịch vụ của cam kết bằng 0 ở năng lực dư và dương thật ở năng lực căng, kèm
   đúng hai điểm năng lực đã đo. Luận điểm phổ quát "cơ chế cam kết rẻ" bị cấm,
   kể cả khi `veh8` đạt gate.

## Hệ quả vận hành

- Ma trận job: Panel A 40 primary (giữ nguyên) + Panel B 40 primary = **80
  primary job**. Robustness giữ nguyên 20 job, chỉ chạy trên Panel A đã prereg.
- Panel B cần grid riêng, derivative tree riêng, driver riêng và execution plan
  riêng; freeze chain phải bind cả hai tree seal. Panel A không được sinh lại.
- Mỗi panel là một arm-pair riêng. Pairing vẫn bind exact arm + policy + unit +
  arrival denominator; bundle của panel này đặt vào manifest của panel kia phải
  fail closed qua `sourceScenarioContentSha256`, đúng cơ chế đã có.
- Denominator: `20 × 108 = 2160` mỗi arm **mỗi panel**. Không gộp hai panel vào
  một mẫu số và không cộng N.

## Cam kết thứ tự chạy

Panel A và Panel B đều được khai **trước outcome**. Được phép chạy tuần tự vì lý
do wall-clock, nhưng **không** được phép quyết định có chạy Panel B hay không sau
khi đã nhìn kết quả Panel A. Bỏ Panel B sau khi thấy Panel A là vi phạm
preregistration và phải ghi thành negative result, không được im lặng.

## Những gì không đổi

Không đổi preregistration payload `H0`, node-cap amendment, analysis-integrity
amendment, Runner-artifact repin, 20-cell panel, 108 arrivals/run, scenario
derivatives, selection/pseudonymization label, solver seeds, endpoint definitions,
strict margin `1,0 pp`, missingness rule, analysis manifest của Panel A hay
negative-result policy. Panel A giữ nguyên byte-for-byte.

## Giới hạn phải giữ

- Hai điểm năng lực **không** cho phép ngoại suy tuyến tính theo số xe. Độ dốc
  B1 cục bộ 9,25 khách/xe trong pilot là diễn giải thăm dò, không phải mô hình.
- `veh4` chưa từng chạy ở mức budget tight/medium/loose trong pilot; kỳ vọng về
  độ lớn ở panel đó là chưa có, và điều đó phải nói rõ thay vì suy từ `veh8`.
- Hai panel khác `scenarioHash`, nên phép so sánh giữa chúng là **between-panel**,
  không phải paired. Chỉ được so sánh sau khi đã chứng minh cùng tập request và
  cùng travel realization; ngay cả khi đó, nó vẫn là diagnostic, không phải test.
- Amendment này là outcome-bearing: mọi run WP9 chạy trước nó không được trộn vào
  cùng estimand.

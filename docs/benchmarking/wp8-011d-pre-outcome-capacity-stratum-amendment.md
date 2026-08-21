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

## Sửa thiết kế

Thêm **capacity stratum** làm nhân tố thiết kế đã khai báo trước outcome:

1. Panel giữ nguyên đúng 20 cell holdout đã đóng băng (5 ngày × 4 demand
   realization, 108 arrivals/cell). Không thêm ngày, không thêm demand file,
   không đổi selection key. Capacity **không** làm tăng N: nó là một stratum
   trong-đơn-vị, không phải đơn vị mới.
2. Hai mức đội xe: `veh8` (đã prereg) và `veh4` (thêm mới). **Không** thêm
   `veh16`: ở mức đó B1 đã bão hoà 128/128 trong pilot nên stratum ấy không có
   khả năng phân giải, và đưa vào chỉ làm loãng báo cáo.
3. Estimand:
   - **Primary** không đổi: `Δ_service_panel` và primary burden trên 20 cell ở
     stratum `veh8`, margin strict `−1,0 pp`. Amendment này không nới, không
     đổi, không thêm điều kiện nào cho gate đã prereg.
   - **Co-primary có điều kiện**: cùng hai đại lượng trên 20 cell ở stratum
     `veh4`, báo cáo riêng với chính margin `−1,0 pp`. Nó không cứu và không làm
     hỏng gate `veh8`; hai stratum được kết luận độc lập.
   - **Capacity interaction** là diagnostic mô tả: hiệu của hai delta giữa hai
     stratum, không có gate, không p-value.
4. Luận điểm được phép phát biểu sau WP9 là luận điểm **có điều kiện**: chi phí
   dịch vụ của cam kết bằng 0 ở năng lực dư và dương thật ở năng lực căng, kèm
   đúng hai điểm năng lực đã đo. Luận điểm phổ quát "cơ chế cam kết rẻ" bị cấm,
   kể cả khi `veh8` đạt gate.

## Hệ quả vận hành

- Ma trận job: 20 cell × 2 arm × 2 stratum = **80 primary job**, thay cho 40.
  Robustness giữ nguyên 20 job, chỉ chạy ở stratum `veh8` đã prereg.
- Mỗi stratum là một arm-pair riêng. Pairing vẫn bind exact arm + policy + unit +
  arrival denominator, cộng thêm `fleetSize`; đổi chỗ stratum phải fail closed
  giống như đổi chỗ arm.
- Denominator: `20 × 108 = 2160` mỗi arm **mỗi stratum**. Không gộp hai stratum
  vào một mẫu số.
- Derivative scenario không đổi. `fleetSize` là tham số execution-plan, nên
  scenario/demand/travel hash của cell giữ nguyên và freeze chain không phải
  materialize lại derivative; execution plan và grid thì phải repin.

## Những gì không đổi

Không đổi preregistration payload `H0`, node-cap amendment, analysis-integrity
amendment, Runner-artifact repin, 20-cell panel, 108 arrivals/run, scenario
derivatives, selection/pseudonymization label, solver seeds, endpoint definitions,
strict margin `1,0 pp`, missingness rule, analysis manifest hay negative-result
policy.

## Giới hạn phải giữ

- Hai điểm năng lực **không** cho phép ngoại suy tuyến tính theo số xe. Độ dốc
  B1 cục bộ 9,25 khách/xe trong pilot là diễn giải thăm dò, không phải mô hình.
- `veh4` chưa từng chạy ở mức budget tight/medium/loose trong pilot; kỳ vọng về
  độ lớn ở stratum đó là chưa có, và điều đó phải nói rõ thay vì suy từ `veh8`.
- Amendment này là outcome-bearing: mọi run WP9 chạy trước nó không được trộn vào
  cùng estimand.

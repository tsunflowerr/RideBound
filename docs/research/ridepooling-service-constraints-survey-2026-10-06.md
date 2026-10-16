# Survey toàn văn: ràng buộc thời gian và giá trị thực tiễn của RideBound

Ngày: 2026-10-06. Quyết định diễn giải: ADR-076.

## 1. Kết luận trả lời người dùng

**Bài toán có nhu cầu thật; mức 10 phút / 1,5 lần / ngân sách 30 giây chưa được
chứng minh là phù hợp hoặc tối ưu cho RideBound.** Đã có nghiên cứu với dữ liệu
doanh nghiệp, nghiên cứu trực tiếp về thay đổi thời gian do nhận khách mới, và
thí nghiệm thực địa về phục hồi quan hệ khách hàng sau sự cố chờ đón. Những
bằng chứng đó xác nhận vấn đề gần với RideBound, chưa xác nhận lợi ích tăng thêm
của đúng ledger C.

Kết luận công bằng hiện tại:

- Không đủ cơ sở gọi toàn bộ hệ thống là đóng góp vô ích.
- Cũng chưa đủ cơ sở nói hệ thống đã tăng lợi nhuận, giảm giá cước, tăng giữ khách
  hoặc có bảo đảm đến đúng giờ ngoài mô hình.
- Nhận xét của giảng viên về khoảng trống nghiệp vụ và giá trị tăng thêm là có
  cơ sở. Cần đối chứng đơn giản và kết quả hành khách chung để lấp khoảng trống.

## 2. Phạm vi tìm kiếm, mức đọc và Claude

Áp dụng luồng tìm đa nguồn, đối chiếu toàn văn của Nature Academic Search và
skill PDF. Tìm từ các paper nền tảng, lần theo trích dẫn về tham số, kiểm DOI/
metadata và ưu tiên PDF của tác giả, nhà xuất bản hoặc kho đại học. Đây là survey
có mục tiêu, **không phải systematic review đã sàng lọc toàn bộ lĩnh vực**.

Corpus chính gồm **9 bài, 210 trang PDF**, kể cả supplement và trang correction
trong các tệp tương ứng. Đã lấy toàn văn, đọc phương pháp/kết quả liên quan,
kiểm trực quan các công thức, bảng và hình được dùng; không tuyên bố kiểm từng
trang của mọi supplement hoặc tái lập thực nghiệm của paper. Mức đọc từng bài,
URL, phiên bản, số trang, byte và SHA-256 nằm trong manifest.

Nguồn bổ trợ Bischoff–Maciejewski–Nagel 2017 có 7 trang vật lý, gồm bìa kho lưu
trữ; overall travel gồm waiting/boarding/riding. Định nghĩa đó không tự xác
nhận endpoint của phương trình đã sửa ở Zwick2022.
[Accepted PDF TU Berlin](https://depositonce.tu-berlin.de/bitstream/11303/8600/1/bischoff_etal_2017.pdf).

Provenance ngoài Git: `E:/RideBoundData/literature-survey-2026-10-06/`:

- `README.md`, `manifest.json`, `references.bib`;
- `pdf/`, `text/`, `notes/`, `renders/`;
- `claude-review-request.md`: gói câu hỏi để Claude phản biện trực tiếp.

Không tìm được kết nối Claude trong công cụ và các đường dẫn đã kiểm. Các lượt
đọc độc lập trong session này là Codex sub-agents, **không gán tên Claude**.
Đoạn trao đổi người dùng gửi được đối chiếu như tài liệu đầu vào, không coi là
phản biện toàn văn đã được xác minh. Handoff đã chuẩn bị; chưa có kết quả từ
Claude trực tiếp trong lượt này.

Hai bài PNAS nền tảng có CrossRef `is-referenced-by-count` lần lượt 1.048
(Alonso-Mora 2017) và 643 (Santi 2014), kiểm ngày 2026-10-06. Đây là chỉ dấu
mức ảnh hưởng trong CrossRef, không phải tổng Google Scholar, xếp hạng chất
lượng hay chứng cứ về hiệu quả RideBound. Bài gần nghiệp vụ được chọn vì định
nghĩa và dữ liệu phù hợp, không chỉ vì nhiều trích dẫn.

## 3. Trước hết phải phân biệt năm loại giới hạn

Gọi r là giờ yêu cầu, P là giờ đón, D là giờ trả, T0 là thời gian đi trực tiếp
ở snapshot chuẩn hóa, D0 là giờ trả đã báo đầu tiên.

| Nghĩa vụ | Công thức minh họa | Câu hỏi nó trả lời |
|---|---|---|
| Cửa sổ chờ đón | P − r ≤ W | Chờ xe bao lâu từ lúc yêu cầu? |
| Thời gian ngồi xe | D − P ≤ αT0 | Ngồi trên xe lâu hơn đi trực tiếp bao nhiêu? |
| Trễ tổng so chuyến trực tiếp | D − r − T0 ≤ Δ | Chờ và đi vòng cộng lại bao nhiêu? |
| Suy giảm lời hứa đầu | D ≤ D0 + l | Trả muộn hơn giờ đã hứa đầu bao nhiêu? |
| Ledger quyết định RideBound | C = Σ abs(p − e) ≤ B | Quyết định mới lệch khỏi phép chiếu giữ tuyến tại cùng trạng thái bao nhiêu? |

Ở dòng cuối, p là ETA kế hoạch đề xuất, e là ETA giữ tuyến từ cùng trạng thái
hiện tại. C không phải tổng thay đổi ETA nhìn thấy trên ứng dụng, sai số dự báo,
trễ thực tế hoặc toàn bộ trách nhiệm nhân quả của doanh nghiệp.

**Code/config được đối chiếu:** panel A đặt W = 600.000 ms; α = 1.500 permille.
`FleetPyManhattanNormalizer.cs:428–445` dựng latest pickup từ r + W và max ride
từ T0 × α; `PhysicalPlanValidator.cs:594` tính ride = D − P, không gồm chờ đón.
T0 được neo vào normalization snapshot, không cập nhật mẫu số theo giao thông.
Đây là synthetic policy overlay, không phải mức chịu chờ đã học từ khách thật.

Nguồn: `E:/Code/Report_INT3508/ridebound-scratchpad/tang3/calib/panelA/grid-panel-a-extra-v1.json`;
code đã đối chiếu ở checkout `C:/Code/RideBound-noworse/`, cùng audit
`notes/local-audit.md`. Checkout hiện tại không được đồng nhất với binary đã
chạy lịch sử. Runner Tier3 cũ có tree digest
`ac251ef32a4865ea3bd3d8d87b4b1bcaccc4462a9b80160b4d205b8d474cff3f`.

## 4. Các bài toàn văn và tham số thực sự

Trang dưới đây là trang PDF vật lý, trừ chỗ ghi trang journal. Các số cùng đơn
vị phút vẫn có thể neo vào thời điểm và đối tượng khác nhau.

| Bài, nguồn PDF sơ cấp | Định nghĩa và thiết lập đã kiểm | Ý nghĩa đối với RideBound |
|---|---|---|
| [Alonso-Mora et al., PNAS 2017](https://autonomousrobots.nl/assets/files/publications/17-alonsomora-ridesharing-pnas.pdf), DOI [10.1073/pnas.1611675114](https://doi.org/10.1073/pnas.1611675114) | Bản có correction, p4/6: W = 2/5/7 phút; Δ = 4/10/14 phút, **gồm chờ + đi vòng**. | Trần trễ tổng 10 phút không xác nhận trần chờ đón 10 phút. Giữ giờ đón khi đổi xe đã có tiền lệ. |
| [Santi et al., PNAS 2014](https://steven-strogatz.squarespace.com/s/quantifying-the-benefits-of-vehicle-pooling.pdf), DOI [10.1073/pnas.1403657111](https://doi.org/10.1073/pnas.1403657111) | Main p2–3, supplement p16: độ trễ tuyệt đối Δ cho đón/trả so chuyến gốc. δ = 1 phút là cửa sổ ghép yêu cầu. | Không có chuẩn 1,5×; phân biệt cửa sổ ghép với mức bất tiện của khách. |
| [Simonetto et al., TRC 2019](https://arxiv.org/pdf/1902.10676v1), DOI [10.1016/j.trc.2019.01.019](https://doi.org/10.1016/j.trc.2019.01.019) | Preprint p14: W = 7 phút, detour = 7 phút; p25: khách Melbourne chọn cửa sổ đón. | Tham số phụ thuộc dịch vụ; bài không tìm mức tối ưu của hai trần này. |
| [Zwick & Axhausen, IEEE ITSC 2020](https://www.moia.io/mobility_analytics/studies/2006_Impact_of_service_design_on_ride-pooling.pdf), DOI [10.1109/ITSC45102.2020.9294289](https://doi.org/10.1109/ITSC45102.2020.9294289) | §IV.C, Table IV p5: thử nhiều mức W, gồm 5/10/12,5/15/20 phút trên dữ liệu MOIA Hamburg. | Nguồn gốc cụ thể cho lựa chọn 10 phút ở bài 2022; không phải chuẩn tâm lý phổ quát. |
| [Zwick et al., TRA 2022](https://mediatum.ub.tum.de/doc/1768112/1768112.pdf), DOI [10.1016/j.tra.2022.09.001](https://doi.org/10.1016/j.tra.2022.09.001) | Eq1 p5, Table2 p6: W = 10 phút; t_detour = min(5 phút + 0,5Tdirect, 15 phút). PDF chưa xác nhận có gồm waiting. | Có intercept và cap; không đồng nghĩa ngồi xe ≤ 1,5T0. Phải kiểm gốc thời gian trước ánh xạ. |
| [Fielbaum & Alonso-Mora, TRC 2020](https://autonomousrobots.nl/assets/files/publications/20-Fielbaum-TRC.pdf), DOI [10.1016/j.trc.2020.102831](https://doi.org/10.1016/j.trc.2020.102831) | §3 p4–5, Appendix A Table6 p18: W = 5 phút; trễ tổng chờ + đi vòng ≤ 10 phút. | Nghiên cứu trực tiếp thay đổi thời gian do nhận khách mới và cách bảo vệ lời hứa. |
| [Schulz & Pfeiffer, OR Spectrum 2026](https://link.springer.com/content/pdf/10.1007/s00291-026-00847-0.pdf), DOI [10.1007/s00291-026-00847-0](https://doi.org/10.1007/s00291-026-00847-0) | p9: deadline = drop offer đầu + l. p28–29, Fig7: quét l = 0/5/10/15/20/25/30 phút. | Đối chứng hạn chót lời hứa đầu đã có; khác ngân sách quyết định cộng dồn. |
| [Cohen, Fiszer & Kim, Management Science 2022](https://maxccohen.github.io/Frustration-Based-Promotions.pdf), DOI [10.1287/mnsc.2021.3991](https://doi.org/10.1287/mnsc.2021.3991) | §3.1 p5–6: ETA error **giờ đón**; thí nghiệm bồi hoàn chọn lỗi >10 phút NYC, >8 phút DC. | Có kết quả doanh nghiệp thực, nhưng can thiệp là credit sau sự cố, không phải trần 30 giây. |
| [Mahfouz et al., Journal of Urban Mobility 2026](https://eprints.whiterose.ac.uk/id/eprint/241354/1/Assessing%20trade-offs%20in%20demand-responsive%20transport%20service%20area%20design.pdf), DOI [10.1016/j.urbmob.2026.100219](https://doi.org/10.1016/j.urbmob.2026.100219) | §3.2.2 p5–6, Table2: W = 10 phút; **chờ + ngồi xe ≤ 1,5Tdirect + 5 phút**. | Có phần đệm 5 phút và tính cả chờ; không thể trích thành RideBound pure onboard 1,5×. |

Lưu ý phiên bản: [correction PNAS 2018](https://doi.org/10.1073/pnas.1721622115)
sửa Fig5D và một số distance trong supplement của Alonso-Mora. Bản PDF MIT cũ
được giữ để bảo toàn provenance; không dùng đồ thị khoảng cách cũ cho claim chi phí.
Simonetto dùng author preprint, không tự coi số trang là trang journal.

## 5. Trade-off có thực sự được nghiên cứu không?

**Có, và không luôn theo trực giác “nới trần thì phục vụ tăng”.**

Zwick–Axhausen 2020 dùng 13.970 yêu cầu của một ngày MOIA Hamburg không nêu ngày
lịch, còn 13.401 sau lọc; supply theo ca. Table IV báo số phục vụ lần lượt **5.908 / 6.868 / 6.382 /
5.789 / 5.643** khi W = 5/10/12,5/15/20 phút. Mean wait ở 10 phút là 7:31,
ở 15 phút là 11:09. Tác giả giải thích thuật toán gán sớm giữ xe cho chuyến xa,
ảnh hưởng cơ hội tương lai. Đây là kết quả của cấu hình/heuristic đó, không phải
định lý về feasible set, hiệu quả sản xuất hoặc W tối ưu mọi thị trường.

Fielbaum–Alonso-Mora 2020, Table5 p17: kết hợp bảo vệ giờ đón đầu và không reject
sau nhận làm rejection **34,5 → 45,6%**, mean wait **1,01 → 0,77 phút**, detour
**0,65 → 0,94 phút**. Bảo vệ một nghĩa vụ có thể chuyển chi phí sang khách bị từ
chối hoặc thời gian trên xe. “Users’ cost” ở bảng là objective mô hình; không
được gọi là lợi nhuận hoặc phúc lợi đo thực. Baseline phần trước cố ý bỏ bảo vệ
pickup, nên mức bất ổn đó không phải tỷ lệ phổ biến của doanh nghiệp.

Schulz–Pfeiffer 2026, Fig7: lợi ích giảm rejection khi nới deadline có xu hướng
giảm dần quanh **10–15 phút**, dưới giả định và dữ liệu sinh của bài. Acceptance
exp(−μRD), μ = 0,5, chọn từ preliminary tests chứ không ước lượng sở thích khách
thực. Không lấy “đầu gối” của đường cong làm SLA chuẩn ngành.

Santi 2014 cho thấy độ chùng thời gian tạo nhiều khả năng chia sẻ và tiết kiệm
thời gian chuyến có khách. Simonetto 2019 Table3 p20: đổi batch 10 → 5 giây
tăng phục vụ **0,37 điểm phần trăm**, đi kèm mean detour tăng **1,32 phút** trong
một cấu hình. Đây là trade-off batching, không phải hiệu ứng riêng của trần.
Các chỉ số vận hành này không tự chuyển thành mức giảm giá hay lợi nhuận.

Zwick 2022 dùng **59.784 request MOIA thật** trong tuần 17–23/1/2022 và shift
plans; doanh nghiệp tham gia tác giả/tài trợ. Tuy nhiên, paper nói kết quả mô
phỏng không trực tiếp tái hiện hoạt động MOIA, và không đánh giá ROI. Mahfouz
2026 giữ tham số thời gian cố định, so vùng phục vụ/đội xe; bài không tìm W tối ưu.

## 6. Doanh nghiệp được lợi gì, và điều gì chưa có bằng chứng?

Bài Via/Cohen là bằng chứng mạnh nhất trong corpus về đầu ra doanh nghiệp:
có ba field experiments, hai thử **bồi hoàn sau sự cố**, một thử credit dịp
kỷ niệm cho khách không gặp sự cố. Future credit cải thiện usage/spending,
tác giả đánh giá campaign tăng revenue sau trừ credit. Hiệu ứng credit rõ hơn
ở sự cố chờ đón; hiệu ứng credit ở nhánh thời gian ngồi xe không có ý nghĩa
thống kê tương tự. Không diễn giải kết quả null thành khách không quan
tâm đi vòng. Phân tích delay–engagement gồm correlation và PSM+DiD observational,
không phải RCT giảm delay. Paper không đo lợi nhuận biên của đổi dispatch.

Từ đó có các **giả thuyết kiểm được**, chưa phải lợi ích RideBound đã đạt:

| Nghiệp vụ có thể cần | Kết quả phải đo thêm | Điều kiện bác bỏ giá trị tăng thêm |
|---|---|---|
| Giảm failure phải bồi hoàn | Số sự cố theo cùng SLA, credit thực trả, hủy, số quay lại | Ledger không giảm sự cố hoặc khoản tiết kiệm nhỏ hơn chi phí mất phục vụ |
| Khách cần lên kế hoạch theo giờ đón/trả | Sai lệch giờ thực so lời hứa đầu, variation hiển thị, hành vi thực | C giảm mà kết quả hành khách không cải thiện so deadline/phạt mềm |
| Bộ phận vận hành cần giải trình quyết định | Thời gian xử lý case, tỷ lệ quyết định sai, khả năng audit từ log thường | Log thường xử lý ngang bằng hoặc tốt hơn, ledger chỉ thêm thao tác |
| Cam kết chất lượng có thể bán như dịch vụ | Nhu cầu/WTP thực, mức phục vụ, giá và chi phí của offer | Khách/operator không chấp nhận chi phí hoặc không mua nghĩa vụ này |

**Phải tách ba nghĩa của “giá”:** giá cước khách trả, chi phí tính toán/điều phối,
và chi phí vận hành xe/cơ hội mất khách. ETA đổi không tự làm bất kỳ giá nào tăng.
Trần cứng có thể giảm pooling, tăng km rỗng hoặc từ chối thêm; ledger có thể
tăng compute/storage. Chỉ số C không có đơn vị tiền.

Điều kiện hòa vốn minh họa: phần biên lợi nhuận giữ lại từ giảm hủy/quay lại,
cộng tiết kiệm credit/support/xe, phải lớn hơn biên lợi nhuận của chuyến mất đi
và chi phí bổ sung. Chưa có dữ liệu cho các thành phần này. Không dùng doanh thu
mỗi chuyến thay cho biên lợi nhuận, hoặc coi refusal count là tiền lỗ chắc chắn.

Văn liệu xác nhận có người quan tâm chất lượng và reliability. Chưa tìm được
bằng chứng doanh nghiệp cần **đúng cách cộng C** thay cho các cách đơn giản.

## 7. Ràng buộc 1,5 lần có quá chặt không?

Với công thức hiện tại, allowance ngồi xe thêm là **0,5T0**:

| T0 | Max ride 1,5T0 | Thời gian đi vòng thêm tối đa |
|---:|---:|---:|
| 5 phút | 7,5 phút | 2,5 phút |
| 10 phút | 15 phút | 5 phút |
| 30 phút | 45 phút | 15 phút |

Đây là phép tính từ contract, không phải dữ liệu chấp nhận của hành khách.
Mức thuần tỷ lệ ít cho phép ghép với chuyến ngắn; bài có intercept/cap hoặc trần
cộng phút phân bổ độ chùng khác. Nhưng **không thể xếp cả contract RideBound
chặt hơn mọi paper**: có bài tính cả waiting; direct-time anchors khác nhau.

Chưa có sensitivity đủ để kết luận hai physical caps là nguyên nhân khiến C
không tạo lợi ích tăng thêm. Nới chúng có thể giúp U nhiều hơn C, đổi loại dịch
vụ đang bán, hoặc tạo thêm ETA churn. Không điều chỉnh chỉ để treatment thắng.

## 8. Kiểm lại các suy luận trong đoạn trao đổi được gửi

Audit lấy source/output đã tồn tại, không chạy lại các cell và không coi phép
đọc này là xác minh độc lập mọi bundle.

- **U không phải pooling hoàn toàn tự do:** cả U và C giữ khóa xe/điểm và ETA
  đón tại confirmation. C thêm cumulative drop decision cap 30 giây. U không
  đại diện ngành đang bỏ mọi cam kết.
- T38 có 3.672 request/arm: U phục vụ 2.434; C phục vụ 2.338. Mất 96 tương ứng
  **2,61 pp trên yêu cầu**, hoặc **3,94% so số U phục vụ**. Chưa có căn cứ gọi
  đây là mức mất lợi nhuận nhỏ hoặc chấp nhận được.
- **73,4% / 1,2%** là signed sum(p − e) chia tổng trễ trong nhóm khách hoàn tất
  và trễ >60 giây. Hai nhánh phục vụ khác tập người, signed sum có bù trừ.
  Đây là phân bổ theo ledger/mô hình trong subgroup, không phải tỷ lệ lỗi nhân
  quả do hệ thống gây ra.
- **108,6 giây** là median nonzero revision charge, không phải mean của một
  lần ghép. Một quyết định ảnh hưởng nhiều khách; ghép có thể charge bằng 0.
- **845 / 158.711 ≈ 0,53%** là tỷ lệ bản ghi prune với budget witness. Có lặp
  qua epoch, search hữu hạn và chỉ witness được gặp; không phải tỷ lệ request
  mất do budget hoặc mức chi phí kinh tế. Physical check trước commitment cũng
  không cho biết mọi constraint đồng thời binding.
- Không có đối chứng thực địa để nói các late rates mô phỏng đã thấp hơn các
  doanh nghiệp thật. Không so số phút hoặc tỷ lệ từ paper có endpoint khác nhau.

Nguồn script và output: `ridebound-scratchpad/optimize/report-extras/constraints_and_pooling.py`;
chi tiết pointer trong `notes/local-audit.md`. H6 fail service gate vẫn giữ nguyên;
T38 không được dùng để cứu confirmatory H6.

## 9. Kết quả forecast mới: có tiến triển, còn giới hạn

Không bỏ qua extension 2026-10-06 được gửi trong session. Đã đối chiếu
`ridebound-scratchpad/optimize/FORECAST-REPORT.md`, plan, report, bảng cell và
independent-review notes hiện có. Plan SHA-256:
`a5524912b834de898046c9b8541e179fd2685ed78a9db6903e72844374d37337`.

Family A thay cách báo ETA trong replay; không đổi chuyến thực hoặc dispatch.
Trong **21 cell forecast-active**, mean late >60 giây so displayed first drop
của C giảm từ **1,94%** với equal-total pad xuống **0,17%** với display forecast;
initial promised drop trung bình muộn thêm **13,7 giây**. Trên **tất cả 34 cell**,
C display là **0,51%**. Không gọi 0,17% là tỷ lệ của toàn bộ thử nghiệm hay bảo
đảm 99,8% cho khách thật; mean cell cũng không tự là pooled passenger rate.

M+-30 và V-30 cùng đạt 0,17% trên tập active: lợi ích forecast không riêng ledger.
Mô hình world dùng một hệ số travel chung toàn thành phố; dữ liệu ngày đã biết
trước plan, ít cụm độc lập. A3 cluster-t **[−3,62; +0,07] pp** chứa 0. Bỏ holiday
làm tập còn hai buổi sáng và cấu trúc A1 không đủ. AI review/GitHub timestamp
không biến việc này thành human peer review hoặc untouched confirmatory sample.

**34/1.471 initial displayed pickups trên 21 cell active** vượt r + 10 phút, dù
feasibility của raw plans không đổi. Cần làm contract/hiển thị nhất quán trước
khi bán offer. Family B hoàn thành **29/32 lần chạy** (16 cell × 2 arm); hai
lần dừng ở 1,67 giây và một ở 2,69 giây chưa được chẩn đoán đầy đủ. Chỉ diễn giải
trong static-matrix wrapper v1.3; chưa kết luận forecast không nên vào dispatcher.

Forecast giúp một giả thuyết về **chất lượng thông báo**, nhưng ETA padding cũng
có thể làm lời hứa dễ đạt hơn mà không đón/trả sớm hơn. Luôn đo mức hứa muộn thêm,
wait/arrival thực và hành vi khách bên cạnh tỷ lệ vi phạm.

FTA ADA Circular §8.5.8 phân biệt điều kiện không dự đoán được với peak traffic
cần tính trong kế hoạch. Văn bản thuộc complementary paratransit theo ADA,
không tự áp dụng cho GrabShare, không cấp quy tắc “ledger ba phần = trách nhiệm
bồi thường/miễn phạt”. Không nhận modeled projection là chứng nhận lỗi pháp lý.
[Nguồn FTA chính thức](https://www.transit.dot.gov/sites/fta.dot.gov/files/docs/Final_FTA_ADA_Circular_C_4710.1.pdf).

## 10. Phép kiểm tiếp theo có thể trả lời điều gì?

Đây là **đề xuất nghiên cứu**, chưa freeze một protocol, đổi contract, cấp quyền
chạy matrix hoặc triển khai sản phẩm.

1. Tách sensitivity physical constraints khỏi ledger cap. Mức chờ 5/7/10 phút
   có tiền lệ trong corpus; chọn giá trị trước khi nhìn kết quả mới. Có thể so
   pure onboard ratio với allowance có intercept hoặc trần phút, nhưng đó là
   những contract khác nhau phải đặt tên và định nghĩa rõ, không gọi tái lập paper.
2. Trong mỗi contract, so cùng U/C với deadline từ lời hứa đầu, current-promise
   cap và soft penalty. Nếu dùng current-promise cap, phải định nghĩa là trần
   mỗi revision so thông báo gần nhất hay trần tổng variation; hai nghĩa khác
   nhau và chưa được chọn làm protocol ở đây. Cùng candidate generator/work cap, fleet, request,
   confirmation locks, travel information và cách báo ETA. Tách forecast thành
   factor riêng để không gán lợi ích forecast cho C.
3. Dùng tất cả request cho completion/rejection; dùng tập common-served cho phân
   tích đôi endpoint và báo selection limits. Báo thêm các phân phối toàn bộ
   khách được phục vụ, không chỉ nhóm trễ hoặc mean cell. Đo actual wait/ride,
   lateness so cùng promise, visible variation, cost xe/km, compute và C.
4. Có ngày/test untouched và phân tích theo cụm phù hợp; không dùng lại T38 đã
   biết để xác nhận giả thuyết mới. Giữ H6/gates/negative receipts lịch sử.
5. Chọn một nghiệp vụ với người vận hành, ngưỡng service/arrival chấp nhận và
   dữ liệu chi phí. Nếu C không cải thiện kết quả chung hơn deadline/phạt mềm
   hoặc chỉ làm đẹp ledger trong khi mất khách, bác bỏ claim cải thiện dịch vụ
   trong nghiệp vụ đã chọn; giá trị audit cần phép kiểm công việc audit riêng.

Không cần hứa nghiên cứu UX để bảo vệ một luận văn hệ thống. Có thể đóng góp
đặc tả, validator, tái lập và phép đo đánh đổi; cần giới hạn đúng phần đã chứng
minh. Muốn nói doanh nghiệp được lợi phải hoàn thành cầu nối từ nghĩa vụ mô hình
đến kết quả vận hành và chi phí/hành vi thật.

## 11. Nguồn chưa đạt mức đọc toàn văn

- Cordeau–Laporte 2007, *Annals of Operations Research*, DOI
  [10.1007/s10479-007-0170-8](https://doi.org/10.1007/s10479-007-0170-8): đã kiểm
  abstract/metadata; chưa lấy primary full PDF. Không ghi thuộc corpus full-read.
- Ichoua–Gendreau–Potvin 2003, DOI
  [10.1016/S0377-2217(02)00147-9](https://doi.org/10.1016/S0377-2217(02)00147-9):
  publisher abstract/introduction về time-dependent routing, chưa lấy PDF tác
  giả do access block. Chỉ dùng làm dấu hiệu cần kiểm giới hạn kết luận forecast.
- Avermann–Schlüter 2019, DOI
  [10.1016/j.rtbm.2019.100420](https://doi.org/10.1016/j.rtbm.2019.100420): chưa
  kiểm nguyên bản lời dẫn “chấp nhận giảm sau 10 phút”. Không nâng thành ngưỡng
  hành vi đã xác minh. Điều này không làm mất tiền lệ tham số từ MOIA2020/2022.

## 12. Tình trạng bàn giao

Survey và corpus là bổ sung chứng cứ. Không sửa source, simulator, runner,
tham số, kết quả lịch sử hoặc PDF khóa luận. Không chạy benchmark hoặc baseline
test mới; không nâng implementation/work-package status. Đã lưu link, DOI,
phiên bản và digest để người dùng/Claude có thể kiểm lại từng kết luận.

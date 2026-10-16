# RideBound: kiểm tra nhu cầu doanh nghiệp và giá trị tăng thêm

Ngày kiểm: 2026-10-05. Liên quan ADR-075. Đây là đánh giá văn liệu, sản phẩm và
chứng cứ đã có; không phải thực nghiệm mới hay nghiệm thu work package.

## 1. Kết luận có điều kiện

Nhận xét rằng RideBound chưa chứng minh được ích lợi doanh nghiệp hoặc UX là
**đúng với hồ sơ hiện tại**. Gọi toàn bộ đóng góp là vô ích thì vượt quá bằng
chứng: chi phí của việc thay lịch đã thông báo là một vấn đề được nghiên cứu và
đã được nhà cung cấp xử lý bằng tính năng sản phẩm. Tuy nhiên, nhu cầu đó
không tự chứng minh người vận hành cần đúng ngân sách tích lũy mà RideBound dùng.

Trạng thái hợp lý: có hiện thực kỹ thuật và kết quả nghiên cứu về một nghĩa vụ
điều phối cụ thể; **giá trị thực tiễn tăng thêm so với cách đơn giản hơn chưa được
xác nhận**. H6 cung cấp bằng chứng bất lợi cho cấu hình chính. T38 chưa thiết lập
ưu thế về trải nghiệm chung. Không kết luận rằng mọi cấu hình đều vô ích, cũng
không dùng số lượng kiểm thử, chứng chỉ hoặc lần chạy để chứng minh giá trị kinh tế.

## 2. Phương pháp và phạm vi đọc

Ba hướng kiểm độc lập: văn liệu học thuật; tài liệu doanh nghiệp/tech blog; phản
biện cơ chế và kết quả nội bộ. Chỉ dùng nguồn sơ cấp để hỗ trợ kết luận kỹ thuật.
Tìm theo các khái niệm ridepooling unreliability, communicated appointment,
replanning disruption, injected solution, first-announced deadline và reliability
preferences. Kiểm cả bằng chứng thuận và tiền lệ có thể làm yếu claim.

Đây là lượt tìm có mục tiêu, không phải tổng quan hệ thống bao phủ mọi bài báo,
patent hoặc hợp đồng doanh nghiệp. Không tìm thấy trong các nguồn đã kiểm bằng
chứng người mua yêu cầu chính xác ledger C của RideBound; điều này không chứng
minh rằng không có người mua như vậy ở bất kỳ đâu.

Phân biệt bốn mức: sở thích trong khảo sát; kết quả mô phỏng; tính năng sản phẩm
hoặc chính sách dịch vụ; kết quả triển khai có đo lợi ích tăng thêm. Các nguồn ở
ba mức đầu không được tự nâng thành ROI của RideBound.

## 3. Nguồn sơ cấp và điều chúng thực sự hỗ trợ

| Nguồn | Nhu cầu hoặc cách xử lý được nêu | Giới hạn đối với RideBound |
|---|---|---|
| [Timefold, minimize disruption](https://docs.timefold.ai/field-service-routing/latest/real-time-planning/real-time-planning-minimize-disruption) | Tái tối ưu lịch dịch vụ hiện trường: cải thiện nhỏ có thể không đáng để đổi lịch đã báo; dùng khóa và phạt mềm thay người/xe/giờ | Bài toán lân cận. So với phân công đầu mỗi lần solve, không xác nhận ngân sách C theo suốt chuyến; tài liệu tính năng không đo ROI |
| [Google Route Optimization, injected constraint](https://developers.google.com/maps/documentation/route-optimization/reference/rest/v1/InjectedSolutionConstraint), [route plan](https://developers.google.com/maps/documentation/route-optimization/route-plan) | Giữ giờ, thứ tự và xe khi cập nhật tuyến; nới khóa theo thời gian hoặc số lượt thăm | Chứng cứ về cơ chế sản phẩm và một cơ chế đối chứng sẵn có; không phải ledger tích lũy của RideBound hoặc nghiên cứu UX |
| [Lyft, scheduled rides](https://help.lyft.com/hc/en-us/all/articles/115013078668-Scheduled-rides-for-passengers) | Một số chuyến đặt trước tới sân bay đủ điều kiện có Lyft Credits khi đón muộn hoặc không ghép được tài xế theo điều khoản | Gắn một kết quả phục vụ thực với nghĩa vụ thương mại; không bảo đảm cho mọi chuyến, không định giá C |
| [Via, product update 2025-04-02](https://ridewithvia.com/resources/via-tells-all-the-new-features-transforming-transit-so-far-this-year) | Ride Groups phân các chuyến trong nhóm vào cùng ca tài xế; công cụ chuyển chuyến khi hỏng xe và lưu ghi chú quyết định | Chứng cứ nhà cung cấp coi tính nhất quán, phục hồi và thời gian nhân viên là vấn đề; các ích lợi được nêu là tuyên bố nhà cung cấp, không phải hiệu quả RideBound |
| [Fielbaum–Alonso-Mora 2020, full paper](https://autonomousrobots.nl/assets/files/publications/20-Fielbaum-TRC.pdf) | Đo bất ổn ridepooling và bảo vệ giờ đón đầu tiên, đồng thời kiểm chi phí từ chối/detour | Có cả cơ chế trước quyết định. Tần suất bất ổn trong mô phỏng không phải tần suất ở doanh nghiệp; baseline bỏ một hạn chế giữ giờ đón của thuật toán gốc |
| [Elmi et al. 2026, TU Delft record](https://repository.tudelft.nl/record/uuid:8d4faffb-7e75-425b-9e53-1ac78e70e2a5) | Shareability shadows chọn hành khách cần bảo vệ; abstract báo giảm cập nhật trong mô phỏng ở cùng rejection rate | Chỉ kiểm abstract trong lượt này. Không phải triển khai, ROI, hoặc tái lập thuật toán bằng các arm T38 |
| [Schulz–Pfeiffer 2026, publisher full text](https://link.springer.com/article/10.1007/s00291-026-00847-0) | Giữ hạn chót trả dựa trên giờ thông báo lúc chấp nhận; cho phép tái tối ưu trong hạn | Nhu cầu về cam kết kết quả đã có tiền lệ đơn giản hơn. Xác suất chấp nhận trong mô hình được giả định, không phải hành vi mua thực tế |
| [Alonso-González et al. 2020, TU Delft record](https://repository.tudelft.nl/record/uuid:7e7284dd-0d14-4b1c-96cf-eb1b363f4fe4) | Khảo sát lựa chọn giả định với 1.006 người về biến thiên thời gian và trễ so với thông báo | Hỗ trợ sở thích về độ tin cậy chuyến đi; không phải sẵn lòng trả tiền cho C, giao dịch thực hoặc retention |
| [König–Grippenkoven 2019/2020, publisher full text](https://link.springer.com/article/10.1186/s12544-019-0391-3) | 410 người trả lời lựa chọn giả định; độ lệch giờ và thông tin thực thời ảnh hưởng lựa chọn | Thông báo thay đổi kịp thời có ích; không được suy rằng cập nhật càng ít luôn càng tốt. Kết quả giới hạn ở mẫu và mục đích chuyến khảo sát |
| [Uber, DeepETA, 2022-02-10](https://www.uber.com/ie/en/blog/deepeta-how-uber-predicts-arrival-times/) | ETA phục vụ ghép chuyến, giá và thông tin; hệ thống được đánh giá về sai số và độ trễ xử lý | Ví dụ đóng góp phía hệ thống gắn với kết quả dùng được. Không xử lý ngân sách thay đổi lời hứa hoặc chứng minh hiệu quả kinh tế của RideBound |

[Changelog Timefold](https://feedback.timefold.ai/changelog/full-constraint-lists-region-aware-login-and-smarter-fsr-replanning)
cũng ghi tính năng phạt thay lịch đã thông báo. Không lấy việc một hãng đã có
tính năng làm bằng chứng rằng một khách hàng cụ thể đã mua nó hoặc đạt mức tiết kiệm nào.

Trường hợp có tên người vận hành: [Optibus nói về Somerset Passenger Solutions](https://optibus.com/case/improving-passenger-experience-for-work-shuttles/)
và nhu cầu giữ xe đưa đón phù hợp ca làm, nhu cầu khách và giờ chạy. Trang mở trực
tiếp hai lần timeout; nội dung sơ cấp có trong kết quả tìm và được agent đọc.
Chỉ dùng nó như trường hợp nhà cung cấp tự báo cáo về quy hoạch/lập lịch. Không
dùng số quy mô hoặc gán đây là triển khai online ledger RideBound.

Các nguồn trên xác nhận ba vấn đề gần: lịch đã báo bị đổi, độ tin cậy giờ phục vụ,
và chi phí thao tác vận hành. **Chưa nguồn nào trong lượt kiểm xác nhận nhu cầu
mua hoặc lợi ích tăng thêm của chính tổng C.** Đây là suy luận từ phạm vi các
nguồn, không phải phát biểu của các tác giả về RideBound.

## 4. Metric hiện tại có khớp điều doanh nghiệp cần không?

Trên mỗi tọa độ ETA, `C_r = sum_t abs(p_rt - e_rt)`, với `p` là ETA đề xuất và `e`
là phép chiếu giữ tuyến tại cùng trạng thái hiện tại. Nó đo phần thay đổi được
hạch toán cho quyết định theo mô hình, không trực tiếp đo trễ đến nơi, tổng biến
động hiển thị, sai số dự báo hoặc trách nhiệm nhân quả toàn bộ hệ thống.

- `C=0` vẫn có thể đi cùng đến muộn và ETA hiển thị thay đổi do phần giữ tuyến.
- Với ETA đã báo `v=600`, giữ tuyến `e=720`, đề xuất sửa `p=600`: biến động hiển
  thị bằng 0 nhưng sổ tính thêm 120 giây. Trần chặt có thể chặn một sửa chữa có ích.
- Tổng trị tuyệt đối tính cả thay đổi sớm hơn. Người đi khám, đi làm hoặc người
  đang chuẩn bị ra điểm đón có thể đánh giá hai chiều khác nhau; không có khảo
  sát nào trong hồ sơ định giá sự đánh đổi này cho C.
- Giữ tuyến và bỏ các chèn chuyến ảnh hưởng khách cũ có thể làm metric đẹp lên.
  Vì vậy phải đánh giá cùng tỷ lệ phục vụ trên toàn bộ nhu cầu và kết quả thực.

Doanh nghiệp có thể cần chính sách hạn chế tác động tùy chọn của điều phối,
nhưng đó là giả thuyết nghiệp vụ cần người sở hữu, ngưỡng và chi phí cụ thể.
Chứng chỉ ở đây là chứng cứ quyết định có phiên bản, không phải chứng nhận pháp
lý hoặc bảo đảm giờ đến thực tế. Log thông thường là đối chứng bắt buộc cho
giả thuyết rằng ledger giúp kiểm tra sự cố tốt hơn.

## 5. Chứng cứ nội bộ: điều bất lợi không được bỏ qua

Nguồn: [hồ sơ khóa luận và kiểm nguồn](../benchmarking/thesis-final-audit-2026-10-05.md),
[kết quả H6](../benchmarking/wp9-confirmatory-result-2026-08-23.md), và các file
`08-thietlap-va-h6.tex`, `08-t38.tex` trong
`E:\Code\Report_INT3508\ridebound-scratchpad\thesis-final-2026-10-04\source\noi-dung`.
Phạm vi binary từng đợt khác HEAD nghiên cứu hiện tại
`C:\Code\RideBound-noworse` (`24aa747147d49db79675d170829e6f3bf3afc3cc`).

| Chứng cứ | Kết quả và cách đọc |
|---|---|
| H6 chính, 20 ô, 2.160 yêu cầu/arm, 8 xe | B1 1.735, C1 1.581 hoàn tất: mất 154, tức 7,13 điểm phần trăm, vượt trần 1 điểm. H6 tổng hợp FAIL. Gói C1 gồm cả khóa xác nhận, không quy toàn bộ chi phí cho trần 30 giây |
| T38 kiểm tra, 34 ô, 3.672 yêu cầu/arm | U 2.434; C-30 và M+-30 cùng 2.338; M+-60 2.347; V-30 2.336. C mất 96 so U: 2,61 điểm phần trăm trên nhu cầu hoặc 3,94% tương đối trên số U, hai mẫu số khác nhau |
| T38 tuân thủ lời hứa riêng | C không vượt sổ riêng; các cổng khác có nghĩa vụ khác. Không so 0% với 14,5% như thể cùng một SLA hoặc như bằng chứng khách hài lòng hơn |
| T38 hành động được chọn | C/M+-30 có cùng chuỗi digest hành động đội xe ở 30/34 ô; C/V-30 ở 27/34. Digest không bao gồm mọi thông điệp, promise, certificate hoặc thời điểm |
| T38 E2 chung | Trung bình theo ô của tỷ lệ khách có tổng biến động ETA trả hiển thị >60 giây: C 5,78297%, M+/V 5,69731%; chênh +0,08566 điểm. Khoảng bootstrap ô danh nghĩa [−0,0082; +0,2218] điểm chứa 0 và chưa xử lý phụ thuộc cụm. Không thiết lập C tốt hơn hoặc C kém hơn có ý nghĩa thống kê |
| Biên hiệu chỉnh T38 | 252,259 giây là biên biến động lời hứa có điều kiện, không phải lateness SLA. 15 ô hiệu chỉnh/34 ô kiểm tra có phụ thuộc và 75 chuyến nguồn chung; không nhận bảo đảm quần thể 90% |

Định nghĩa digest: `tang3/pilot/pilot_analyze.py:33–56` lọc action
`requestAccepted/requestRejected/requestDeferred/vehiclePlanUpdated` trong quyết
định đã ACK, canonical hóa, sắp action trong từng quyết định và băm. Caller
`tang3/t38/t38_analyze.py:93,185–187` giữ thứ tự quyết định nhưng bỏ `simTimeMs`
trước khi so chuỗi. Không chỉ so số phục vụ, cũng không so toàn bộ quỹ đạo có giờ.

Chưa có doanh thu, lợi nhuận, khiếu nại, hủy chuyến, giữ khách, chi phí hỗ trợ,
thời gian điều phối viên hoặc ngân sách do người vận hành xác nhận. Không đổi
số chuyến thiếu thành tiền bằng giả định mỗi chuyến có cùng biên lợi nhuận.
Miễn trừ tự động không tự chứng minh có nhân viên phải xử lý.

## 6. Đóng góp còn bảo vệ được

- Kỹ thuật: lõi có phiên bản, ledger, validator, replay và evidence. Đây là
  hiện thực cụ thể, không tự chứng minh ích lợi kinh tế hay ưu tiên novelty.
- Nghiên cứu: đặc tả các nghĩa vụ khác nhau, điều kiện và phản ví dụ của chúng,
  đo chi phí đánh đổi và giới hạn nơi các metric trùng nhau. Kết quả âm có giá
  trị khi câu hỏi và đối chứng đủ rõ; không bảo đảm chất lượng công bố.
- Kinh tế/UX: chưa được xác nhận. Đẳng thức no-op và bất đẳng thức tam giác là
  hệ quả đơn giản của định nghĩa; số lượng định lý hoặc audit không thay kết quả
  người dùng. Không nhận reliability, khóa, giữ hạn chót hay bảo vệ từng khách là mới.

## 7. Việc cần kiểm tiếp và điều kiện bác bỏ

Đây là đề xuất nghiên cứu, **không cấp quyền chạy** thực nghiệm hoặc WP mới.

1. Chọn một người vận hành và một người có quyền quyết định chính sách. Lấy
   sự cố thực đã xảy ra, điều khoản đang dùng, chi phí hậu quả và mức cải thiện
   tối thiểu đủ để họ nhận công nghệ. Hỏi cả khi nào đổi ETA/đổi xe có ích. Nếu
   không có vấn đề đủ tốn kém hoặc không ai cần chính sách C, dừng claim doanh nghiệp.
2. So với các lựa chọn đang giải quyết vấn đề: hạn chót từ lời hứa đầu; khóa
   gần giờ phục vụ; phạt mềm thay lịch; RideBound. Giữ cùng điều kiện vật lý,
   compute, khóa và quyền tiếp cận dữ liệu; ghi rõ nếu không thể ghép tương đương.
   Đo toàn bộ nhu cầu gồm từ chối/hủy, giờ đón/trả thực, chờ/detour, cập nhật có
   ích, chi phí hỗ trợ, công lao động và biên lợi nhuận quan sát được.
3. Nếu đề xuất giá trị kiểm tra sự cố: dùng nhân viên thực và tình huống chưa
   dùng phát triển, so chéo có làm mù giữa log cấu trúc thông thường và cùng log
   thêm evidence RideBound. Đo chẩn đoán đúng, bỏ sót và thời gian trả lương.
   Bác bỏ trường hợp kinh doanh này nếu tiết kiệm có căn cứ không đủ bù triển khai.
4. Nếu đề xuất giá trị chặn quyết định: cần thử nghiệm hiện trường có đối chứng
   chủ động, chẳng hạn phân ngẫu nhiên theo ngày đội xe hoặc switchback. Khóa
   budget và tiêu chí kết quả thực trước khi xem outcome; xác định cỡ mẫu từ
   hiệu ứng có ý nghĩa với người vận hành. Bác bỏ cấu hình nếu lợi ích không
   bù mất năng lực hoặc thường chặn sửa chữa có ích. Không bịa ngưỡng, p-value
   hoặc quy đổi doanh thu khi chưa có dữ liệu.

Chạy thêm nhiều ô mô phỏng có thể kiểm cơ chế, nhưng không thay được xác nhận
nhu cầu hoặc phép đo giá trị tăng thêm với một bên vận hành thực.

## 8. Kiểm bàn giao

Đây là thay đổi tài liệu. `git diff --check` không có lỗi whitespace; Git chỉ
báo chuyển dòng CRLF/LF ở các tệp đã có. Năm tài liệu liên quan kiểm được 157
liên kết nội bộ, không thiếu đích; UTF-8 và code fence hợp lệ. Không chạy lại
`.NET`/BeGo hoặc ghi baseline mới vì không đổi code.

PDF đã bàn giao không đổi, SHA-256
`e7a4712663ad2167f2bbbffd7330fa0c64a580964cff558b3d3dc2ecba14454c`.
Không sửa bản gốc khóa luận, dữ liệu, bộ thực thi hoặc các tệp dirty thuộc người dùng.

# Rà soát mức hoàn tất và điểm yếu khi bảo vệ — 16/10/2026

## Kết luận

**Bản thảo đầy đủ và phép so bổ sung đã hoàn tất; chưa nên đóng trạng thái
“hoàn thành toàn bộ, sẵn sàng nộp/bảo vệ”.** Hai việc này khác nhau. Bản hiện tại
có một đóng góp hệ thống/phương pháp thực nghiệm có thể trình bày trong phạm vi
hẹp, nhưng còn thiếu giải thích chọn mẫu, cần xử lý cách diễn giải khoảng tin
cậy, và chưa xác nhận mẫu trình bày áp dụng. Kết hợp dự báo vào lời hứa đã được
đánh giá bằng phát lại; kết hợp dự báo tham gia quyết định điều phối chưa có
một kết quả đầu-cuối hoàn chỉnh để nhận là hệ thống triển khai đã hoàn tất.

Đánh giá này không thay nhận xét/điều kiện chấp thuận của GVHD, khoa hay hội đồng.
Không ước lượng phần trăm hoàn thành hoặc điểm bảo vệ.

## Bản được kiểm và phạm vi

- `E:/Code/Report_INT3508/ridebound/main.pdf`,77trang, SHA-256
  `26c8dea1334e68091b421a831bf14e809e76a396e0444b75398d662a8921dd69`.
- Đối chiếu22tệp payload đã giao và13mục manifest kết quả gốc: không lệch hash.
  Kiểm lại số gộp bốn cấu hình từ TSV;21/34kịch bản bật dự báo ở mỗi cấu hình.
  Các phép kiểm chỉ đọc, kết quả ở `trace-evidence.json`.
- Đọc các phát biểu mục tiêu/phương pháp/kết quả/hạn chế, truy lại mã chuẩn hóa,
  mã ngày nguồn dự báo và mã tính khoảng theo cụm. Đã xác nhận hash chuẩn hóa
  hiện tại bằng đúng hash ghi trong49report; mọi report đều chọn108yêu cầu.
- Đối chiếu công thức7–11 của Hörl–Zwick từ PDF gốc có sẵn; mục6 của Fielbaum–
  Alonso-Mora từ PDF gốc; bài Schulz–Pfeiffer trên trang nhà xuất bản và PDF;
  metadata arXiv2605.11798 trên arXiv. Đây không phải rà mới toàn bộ18nguồn.
- Dùng hồ sơ kiểm bố cục77trang đã có, PDF hiện tại không đổi; không nhận đã
  chạy lại mọi số lịch sử từ transcript, mô phỏng, học máy hay .NET.
- Chỉ tạo hồ sơ kiểm trên C. PDF/nguồn luận văn, code, dữ liệu thực nghiệm,
  frozen files, lịch tự động và lịch sử Git không bị sửa trong lượt rà này.

## Những việc thực sự đã xong

| Việc | Trạng thái và giới hạn |
|---|---|
| Bản thảo năm chương, tóm tắt Việt/Anh, hình/bảng/phụ lục | Có đầy đủ;77trang, build/visual QA đã có; không đồng nghĩa đã được GVHD chấp thuận |
| Luật C và sổ theo khách | Đặc tả/cài đặt/kiểm trong mô hình đã nêu; giới hạn phần dời tính cho điều phối, có điều kiện giữ kế hoạch hợp lệ |
| So hai thứ tự chọn phương án |68/68lượt mới và phân tích đã xong; kết quả cùng68lượt lịch sử, đầy đủ đánh đổi; thăm dò, hai bản chạy |
| Dự báo ở lớp lời hứa | Có phép tính lại trên hành trình đã ghi; không thay việc xe chở ai hoặc giờ trả thực tế |
| Dự báo tham gia điều phối | Có thử nghiệm nhưng29/32hoàn tất,3dừng; chưa đủ gọi tích hợp đầu-cuối hoàn chỉnh |
| Cộng dồn so với giới hạn từng lần | Khác nhau về định nghĩa; chưa có nhánh đối chứng trực tiếp về kết quả phục vụ |
| Toàn bộ roadmap RideBound ban đầu | Chưa hoàn thành: không nâng WP12/cross-system/generalization; kết quả âm của nghiên cứu trước giữ nguyên |
| Hồ sơ nộp/bảo vệ | Chưa xác nhận đầy đủ mẫu áp dụng, xác nhận GVHD, bản tóm tắt riêng, slide và demo tái lập của phiên bản hiện tại |

## Các điểm cần xử lý trước khi đóng bản nộp

### R01 — Cần công khai luồng chọn mẫu và11tổ hợp bị loại

**Mức: cần sửa phần phương pháp.** Chương4mục4.1.1–4.1.2 nói có49kịch bản,
108yêu cầu/kịch bản từ mẫu10%, nhưng chưa giải thích đầy đủ quá trình từ60tổ
hợp ban đầu xuống49. Nguồn `prepare_panelA.py:1–11,38–48` ghi5ngày×4mẫu×3khung
=60;11tổ hợp không dựng được108yêu cầu trong giới hạn96nút sau quy tắc nhãn
gốc và tối đa3nhãn thử tiếp đã định trước, nên bị loại **trước mô phỏng**.

Nguồn chuẩn hóa `FleetPyManhattanNormalizer.cs:647–755` chọn cụm nút theo độ
phủ cặp đi–đến và tần suất đầu mút, rồi lấy108hàng xếp theo HMAC nằm trong
cụm. Đây không phải mẫu ngẫu nhiên đơn giản của mọi chuyến Manhattan.
Ví dụ `d20181114-s10-r3-w07`:24.974hàng đầu vào của tệp ngày,2.680hàng đủ điều
kiện trong bộ lọc,108được chọn. Không diễn giải22.294hàng ngoài bộ lọc thành
dữ liệu hỏng; chúng có thể nằm ngoài khung giờ hoặc không đủ điều kiện khác.

`selection-trace.json` xác nhận49report và hash5tệp nguồn đúng
`34517a83…4d90`, không chỉ suy từ tên tệp. Đã có giới hạn chuyến ngắn/tải thấp
ở4.11, nhưng cần thêm sơ đồ **60→49→34kiểm tra+15hiệu chỉnh**, mô tả chọn nút,
quy tắc loại, bảng các ngày/khung bị thiếu và ảnh hưởng đến tính đại diện.

Đây không phải11lượt thất bại trong68lượt bổ sung, cũng chưa có bằng chứng
chọn bỏ theo kết quả phục vụ. Điểm yếu là tính đại diện và mức công khai phương
pháp; không được tự phục hồi11tổ hợp hoặc đổi tập đo để làm đẹp kết quả.

### R02 — Khoảng tin cậy95% cần hạ mức diễn giải

**Mức: cần sửa lập luận thống kê.** Chương4mục4.7, trang in46/vật lý59,
`08-chuong4.tex:967–971`, báo khoảng Student từ8nhóm ngày–khung trên21kịch
bản. `forecast_family_a.py:168–186` phân tầng theo khung, coi các nhóm
ngày–khung trong các tầng khi cộng phương sai, df=8−3=5.

Các khung của cùng ngày/mẫu có thể chung3–20yêu cầu, dùng cùng diễn biến ngày
và cùng các hồ sơ nguồn. Chỉ có3ngày được bật dự báo. Gom theo ngày–khung
chưa tự loại được phụ thuộc giữa các khung trong cùng ngày; con số8không
biến thành8ngày độc lập. Khoảng hiện tại có thể được tái lập về số học nhưng
giả định dùng nó để suy rộng còn yếu. Không đủ cơ sở chỉ từ việc khoảng không
chứa0để kết luận hiệu quả ngoài mẫu đã xem.

Cách chốt phạm vi: giữ số đếm/hiệu từng ngày và mức độ nhạy, gọi rõ đây là
phép thăm dò có điều kiện; đưa khoảng cũ kèm giả định vào phần bổ trợ hoặc
bỏ cách dùng nó làm bằng chứng khẳng định. Nếu làm phân tích suy luận mới,
cần đặc tả đơn vị độc lập phù hợp và báo rõ rằng số ngày rất ít; không tự coi
gom theo3ngày là chữa được mọi vấn đề. Không sửa output phân tích gốc.

Nguồn phương pháp bổ trợ: Cameron–Miller, phần giả định độc lập giữa cụm và
ít cụm: https://cameron.econ.ucdavis.edu/research/Cameron_Miller_JHR_2015_February.pdf.
Đây là suy luận áp dụng vào thiết kế hiện tại, không phải khẳng định bài đó
đã đánh giá dữ liệu RideBound.

### R03 — “Kết hợp dự báo” cần gắn trạng thái thực thi rõ hơn

**Mức: quan trọng khi bảo vệ.** Tóm tắt nói luật C đi cùng dự báo và sổ;
đoạn sau có ghi đúng là tính lại. Chương3mục3.5 cũng nói Dđược tính sau chạy,
không quay lại phép kiểm. Cần một sơ đồ trạng thái dễ thấy, tránh người đọc
hiểu tất cả kết quả là của bộ điều phối đã dùng dự báo khi ra quyết định.

`display_layer.py:1–19,59–64` và `promise_stretch.py` xác nhận phần chính là
phát lại lời hứa; `preflight_world_v1_3.py` là thử nghiệm khác, đổi bản thời
gian đi gửi cho Runner. Chương4mục4.7ghi3/32lượt dừng do kế hoạch đón không
khả thi, và mục4.11/5.3ghi34/1.471lời hứa đón hiển thị vượt hạn đón trong khi
phép kiểm dùng giờ chưa dự báo.

**34là lời hứa hiển thị, không phải34khách thực tế bị đón muộn vì dự báo.**
Ba lượt dừng cũng không được bỏ khỏi tỷ lệ hoàn tất của phép thử tích hợp.
Các giới hạn đã ghi là đúng, nhưng chúng là việc chưa hoàn tất nếu mục tiêu
được hiểu là một hệ thống dự báo–điều phối vận hành đồng nhất. Muốn nhận mục
tiêu đó phải xử lý hợp đồng giờ đón, kiểm hồi quy và đánh giá đầu-cuối mới theo
kế hoạch riêng. Nếu chốt khóa luận theo phạm vi hiện tại, gọi chính xác là
“luật C cùng đánh giá lớp lời hứa theo dự báo trên hành trình đã ghi”.

### R04 — Chưa xác nhận mẫu trình bày áp dụng

**Mức: cần chốt trước nộp, không tự đổi mẫu.** Nguồn công khai UET cho chương
trình chuẩn/CLC (tải từ trang trường,9trang; p1–2và6) nêu lề trên2,5cm,
dưới3cm, giãn dòng1,3, cách đoạn6pt và xếp tài liệu theo ABC họ tác giả nước
ngoài. Bản hiện tại ở `main.tex:29–34` dùng trên/dưới2cm, giãn1,5,cách đoạn2pt;
tài liệu tham khảo chưa theo ABC. Thứ tự một số phần đầu/cuối cũng khác mẫu.

Bản đang theo mô hình báo cáo DACN do người dùng chọn. Hướng dẫn công khai
nằm ở đường dẫn2017, chưa xác nhận đây là mẫu cuối áp dụng cho khóa/ngành/đợt
nộp của người dùng; vì vậy đây là **chênh lệch cần xác nhận**, chưa tự kết luận
bản hiện tại sai quy định hiện hành. Giữ tên đề tài và GVHD đã chốt; đối chiếu
quyết định đăng ký thực tế trước nộp.

Nguồn: https://uet.vnu.edu.vn/wp-content/uploads/2017/07/Quy-%C4%91i%CC%A3nh-v%C3%AA%CC%80-tri%CC%80nh-ba%CC%80y-kho%CC%81a-lu%C3%A2%CC%A3n-t%C3%B4%CC%81t-nghi%C3%AA%CC%A3pCLC.pdf.

## Các điểm thiếu bằng chứng, có thể giữ làm giới hạn nếu không nâng kết luận

### R05 — Lợi ích riêng của cộng dồn chưa được đo trực tiếp

Chương4mục4.5và5.1đã nói đúng:7lịch sử của nhánh không trần phân biệt hai
định nghĩa ở mức giả định120giây; một bản ghi loại phương án do cộng dồn ở
trần30giây không xác nhận cải thiện phục vụ. Chưa có đối chứng giới hạn từng
lần được chạy đầu-cuối. Không được dùng hai quan sát này để nói cộng dồn
hiệu quả hơn. Có thể bảo vệ phần đặc tả, tính chất và cài đặt; lợi ích vận
hành riêng vẫn mở. Ngưỡng7<10là sàng lọc nguồn lực, không là kiểm định thống kê.

### R06 — Đối chứng gần nhất chưa được tái hiện đầy đủ

Hörl–Zwick đã có mốc tuyến cũ tính lại và ràng buộc cứng, Fielbaum–Alonso-Mora
đã đo và kiểm soát bất ổn, Schulz–Pfeiffer đã có hạn chót từ lời mời. Bản
hiện tại ghi tiền lệ này và không nhận thuật toán mới là phù hợp.

Nhưng luật hạn chót trong mô phỏng chỉ mượn một thành phần; chưa chạy hạn
chót nới theo mốc giữ kế hoạch. Do đó chưa có bằng chứng RideBound tốt hơn
toàn bộ các phương pháp bài báo. Không nhất thiết chạy lại để hoàn tất phạm
vi hẹp, nhưng khi bảo vệ phải nói rõ cái gì được kế thừa và cái gì được đo.
Chi tiết nhỏ cần diễn đạt chuẩn ở2.3: công thức8giờ đón của Hörl–Zwick là phần
mở rộng ở chiến lượcC; không gán tất cả công thức7–8cho riêng chiến lượcB.

### R07 — Dự báo ít ngày và đã xem dữ liệu, chưa có xác nhận mới

Trích trực tiếp hàm ngày nguồn bằng AST xác nhận không dùng ngày tương lai
ở cấu hình chính:14←12,13;15và16←12,13,14. Tuy nhiên đó chỉ là2–3ngày nguồn,
ba ngày đánh giá có bật dự báo; hai ngày cuối tuần không bật. Loại thủ công
ngày tuyết là lựa chọn của nghiên cứu, chưa là bộ phát hiện sự kiện tự động.
Hệ số hiện tại được coi là biết chính xác trong mô phỏng, chưa đo độ trễ/sai
số quan trắc của một hệ thống giao thông thật.

Không thấy lỗi dùng ngày tương lai trong quy tắc chính đã kiểm. Điều thiếu
là đánh giá dự báo trên các ngày mới, kiểm sai số/độ hiệu chuẩn và dữ liệu
khác điều kiện. Việc xem trước cả dữ liệu và thử nhiều cách làm khiến kết
quả vẫn là thăm dò. Dữ liệu TLC mới đọc mẫu chưa nằm trong bằng chứng này.
Thêm ML phức tạp không tự khắc phục thiết kế đánh giá.

### R08 — Phép so bộ chọn dùng hai bản chạy

Mục4.6.3ghi rõ bốn cặp kiểm hành vi chưa chứng minh tương đương toàn bộ.
Đó là giới hạn hợp lệ; không đọc chênh lệch26/119hoặc tương tác180như tác động
nhân quả thuần của một mức xếp hạng. Phép so nội bộ trong từng thứ tự có đối
chứng cùng bản; phép so giữa hai thứ tự yếu hơn do khác bản chạy. Nếu cần
kết luận mạnh hơn phải có đối chiếu phiên bản đầy đủ/phép chạy cùng bản theo
kế hoạch riêng, không tự chạy lại hay sửa kết quả đang khóa.

### R09 — “Chi phí” và “trực tuyến” chưa kèm đủ thước đo vận hành

Chi phí kế hoạch là tổng thời lượng tuyến còn lại tại mỗi lượt; tổng qua
nhiều lượt sẽ đếm lại phần tuyến, không phải chi phí vận hành thực. Nhiều
khách đi chung cũng không tự suy thành tiết kiệm xăng, VHT/VMT hoặc lợi nhuận.
Mục3.7.2chỉ báo thời gian cả lần chạy, không có p50/p95/p99độ trễ mỗi quyết
định, phần riêng kiểm trần và so runtime cùng điều kiện của hai bộ chọn mới.

Nên bổ sung các thước đo từ nhật ký sẵn có nếu đủ trường, hoặc giữ kết luận
chỉ ở phục vụ/đi chung/sửa giờ. Không dùng thời gian chạy song song cả job làm
độ trễ phản hồi thực tế. “Trực tuyến” trong mô hình đến theo thời gian vẫn có
ý nghĩa, nhưng chưa đồng nghĩa đã đạt một hạn đáp ứng thời gian thực.

### R10 — Người nghe dễ hiểu sai cam kết được bảo vệ

Trong3.2–3.6, trần đặt lên tổng|c|,c=p−e; nó không đặt trực tiếp lên thay đổi
giờ hiển thịD, tổng|z| hoặc a−D0. Dự báo cũng có thể đổiDgiữa các lần công bố.
Số khách bị điều phối sửa giờ trong bảng mới là chỉ số phầnc, không đủ mô tả
toàn bộ số lần thông báo mà khách thực sự nhìn thấy sau dự báo.

Cần một ví dụ bảo vệ giải thích bốn thời gian khác nhau: hạn đón600giây,
trần phần điều phối30giây, ngưỡng đánh giá muộn60giây và trần120giây của phép
sàng lọc. Việc người dùng chịu chờ vài phút không tự xác định ngân sách sửa
lời hứa sau xác nhận. Cộng cả độ dời sớm là lựa chọn kiểm ổn định hai chiều;
khóa luận chưa xác nhận khách định giá hai chiều như nhau.

### R11 — Điều kiện mô phỏng còn thuận lợi và khác dịch vụ thật

Chỉ108yêu cầu,8xe,96nút, thời gian dừng0trong mẫu đã kiểm, hệ số giao thông
chung toàn thành phố và tải thấp. Các ràng buộc khóa cùng bộ sinh chèn thêm
vẫn góp phần vào kết quả; trần chưa được tách khỏi mọi thành phần khác.
Không có nhu cầu hủy, người dùng từ chối, tái gán khách hoặc mạng đường tắc
cục bộ. Đây là giới hạn đã được công khai; cần giữ, không gọi chúng là lỗi
code hay yêu cầu mở toàn bộ thành phố để có thể viết khóa luận.

### R12 — Hồ sơ tái lập và kiểm thử chưa tương đương một bản phát hành

Có source/pin/log/checksum và kiểm độc lập nội bộ, nhưng bộ hồ sơ khóa luận
còn phụ thuộc đường dẫnC/E; README hiện tại chủ yếu hướng dẫn buildPDF.
Chưa xác nhận một gói chạy từ máy sạch bởi người khác, giấy phép/phân phối
dữ liệu đi kèm hoặc bản phát hành/DOI. RoadmapWP12có mục artifact/demo; không
được coi PDF77trang là đã hoàn thành cả gate đó.

Baseline .NET của bản ưu tiên chi phí là1.033/1.034, một bài thất bại đòi cây
có thay đổi trên lần chạy cây sạch. Bản hiện tại ghi đúng; chưa có căn cứ từ
lỗi đó để kết luận cơ chế C sai, nhưng cũng không gọi cả bộ kiểm thử xanh.
Không đổi code/test hoặc tạo bản dirty chỉ để có con số đẹp trong lượt kiểm.

### R13 — Cần tách phạm vi khóa luận hiện tại với chương trình nghiên cứu ban đầu

Hiến chương ban đầu có nhiều chiều cam kết, gần baseline về phục vụ, BeGo,
FleetPy và simulator thứ hai. Hiện có kết quả âm ở thử nghiệm service gate
ban đầu và cross-system chưa thành công trên bộ đại diện. Bản viết đã thu
hẹp xuống luật C và đánh đổi trong FleetPy. Việc thu hẹp có thể hợp lý, nhưng
“hoàn thành toàn bộ RideBound” sẽ sai.

Nên có ma trận đóng góp→đã cài→đã đo→chưa chứng minh, chốt với GVHD đúng phạm
vi đăng ký. Không cần dồn mọi lần chạy lịch sử vào luận văn hoặc biến kết
quả mới thành cách cứu giả thuyết cũ. H6/E1 gốc phải giữ nguyên trong hồ sơ.

## Thứ tự xử lý đề nghị

1. Bổ sung chọn mẫu60→49→34/15, cách chọn nút/hàng và lý do loại11tổ hợp.
2. Chốt diễn giải thống kê: số đếm và kết quả theo ngày là chính; khoảng95%
   kèm giả định, không dùng làm xác nhận ngoài mẫu đã xem.
3. Thêm ma trận trạng thái từng thành phần và cách hiểu lời hứa: Cđang chạy,
   dự báo phát lại, tích hợp điều phối còn vấn đề; chốt đóng góp hẹp với GVHD.
4. Xác nhận mẫu UET áp dụng rồi mới sửa lề/giãn dòng/ABC/thứ tự phần; giữ bản cũ.
5. Đóng gói recipe tái lập, demo phiên bản đúng, slide và câu trả lời bảo vệ;
   xác nhận các giấy tờ/tóm tắt/yêu cầu nộp của đúng đợt. Chưa tìm thấy bộ này
   trong các thư mục luận văn/tiếp phiên/viết lại đã kiểm, không suy rằng chúng
   không thể tồn tại ở nơi khác.
6. Chỉ mở nghiên cứu mới nếu muốn nâng kết luận: ưu tiên sửa hợp đồng dự báo
   và đánh giá trên ngày mới; không thêm ML hoặc chạy hàng loạt chỉ để tăng số
   dòng/kết quả. Nhánh cộng dồn-vs-từng-lần chưa được tự mở.

Các bước1–5là hoàn thiện bản nộp và hồ sơ bảo vệ. Những nghiên cứu trong bước6
không phải điều kiện tự động cho mọi khóa luận CNTT; chúng cần thiết khi mục
tiêu đăng ký hoặc kết luận mong muốn đòi bằng chứng mạnh hơn phạm vi hiện tại.

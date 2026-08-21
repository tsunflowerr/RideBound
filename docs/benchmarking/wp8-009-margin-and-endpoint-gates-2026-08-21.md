# Margin và cổng endpoint — RB-WP8-009

Biên service giữ nguyên **1,0 điểm phần trăm**, treatment−B1, không nới sau khi
pilot cho thấy C1 có khả năng trượt. Trên fixed panel WP9, cổng là phép so sánh
exact aggregate `Δ_service_panel > −1,0 pp`; không gắn một CI dân số giả vào 20
cell hữu hạn.

Primary duy nhất là `total_decision_induced_burden_ms` ở mức run, tính bằng tổng
pickup-ETA và drop-ETA decision delta trên toàn lifecycle. Tỷ lệ hoàn thành là
cổng đồng thời, không phải secondary.

Ba điều kiện chất lượng bắt buộc:

1. Báo riêng phần giảm do lock pickup theo định nghĩa và phần drop-ETA kiếm được
   trên chiều không khóa; pilot ba cell là 17,98%/82,02%.
2. Kèm `materialEtaRevisionCount` và số decision frame có delta khác 0 vì tổng
   millisecond có đuôi nặng.
3. Exogenous burden là falsification metric: hai arm phải dùng cùng arrival/travel
   realization; chênh khác dự kiến phải được điều tra như lỗi pairing/mapping.

Quy đổi thiếu hụt ra số xe chỉ là diễn giải. Nó không sửa margin, không tham gia
gate và không biến pilot thành bằng chứng production.

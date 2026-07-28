# Thuật ngữ RideBound bằng tiếng Việt dễ hiểu

| Thuật ngữ | Giải thích |
|---|---|
| Ridepooling | Nhiều hành khách có chuyến tương thích đi chung một xe |
| DARP | Bài toán lập tuyến xe phải đón và trả từng hành khách, thường có thời gian/capacity |
| Online/dynamic | Yêu cầu xuất hiện khi hệ thống đang hoạt động, không biết hết từ đầu |
| Rolling horizon | Mỗi khi có thông tin mới, tối ưu lại phần kế hoạch còn ở tương lai |
| Active-route insertion | Chèn yêu cầu mới vào route của xe đang chạy |
| Decision epoch | Mốc hệ thống được phép ra quyết định lại |
| Route prefix | Phần route đã chạy hoặc đã bị khóa |
| Route suffix | Phần route tương lai còn có thể thay đổi |
| Commitment/promise | Thông tin hệ thống đã hứa với khách: ETA, xe, điểm, thứ tự |
| Revision | Một lần hoặc một mức thay đổi lời hứa |
| Churn | Kế hoạch đổi qua đổi lại nhiều lần |
| Path-dependent | Chất lượng phụ thuộc cả lịch sử thay đổi, không chỉ trạng thái cuối |
| Ledger | Sổ cái ghi nối tiếp mọi lời hứa và phần budget đã tiêu |
| Budget | Giới hạn tổng thay đổi hoặc số lần đổi được phép |
| Total variation | Tổng độ lớn từng bước thay đổi; đi tới rồi quay lại vẫn bị cộng |
| Switch count | Số lần đổi giá trị rời rạc như xe hoặc điểm đón |
| Hard constraint | Điều kiện không được vi phạm trong vận hành bình thường |
| Soft penalty | Vi phạm/không tốt vẫn có thể chọn nhưng bị cộng điểm phạt |
| Freeze horizon | Khoảng thời gian gần pickup mà một số quyết định bị khóa |
| Certificate | Bản ghi máy kiểm tra được rằng decision thỏa invariant/budget |
| Witness | Chi tiết nhỏ chỉ đúng request/dimension/epoch gây lỗi |
| Invariant | Điều luôn phải đúng, ví dụ pickup trước drop-off |
| Exogenous | Nguyên nhân bên ngoài quyết định, ví dụ traffic đổi |
| Endogenous/decision-induced | Thay đổi do thuật toán đổi plan |
| Customer-visible | Thay đổi cuối cùng khách nhìn thấy, bất kể nguyên nhân |
| Candidate plan | Một phương án route cụ thể đang được cân nhắc |
| Candidate generation | Quá trình sinh các phương án để solver chọn |
| Solver | Chương trình tìm phương án tốt trong tập constraint |
| CP-SAT/MILP | Hai họ mô hình tối ưu với biến rời rạc/số nguyên |
| Lexicographic objective | Ưu tiên mục tiêu theo thứ tự; chỉ xét mục tiêu sau khi giữ mục tiêu trước |
| Epsilon-constraint | Khóa một mục tiêu trong ngưỡng rồi tối ưu mục tiêu khác |
| Baseline | Phương pháp đối chứng để biết cải tiến có thật hay không |
| Ablation | Bỏ từng thành phần để biết thành phần nào tạo tác dụng |
| Replay | Chạy lại cùng event stream để so policy hoặc kiểm tái lập |
| Deterministic | Cùng đầu vào/version/seed cho cùng đầu ra |
| Manifest | Hồ sơ mô tả chính xác data, config, version, seed của một run |
| Paired experiment | Hai policy chạy đúng cùng scenario/seed rồi so theo cặp |
| Confidence interval (CI) | Khoảng thể hiện độ không chắc chắn của ước lượng |
| Non-inferiority | Kiểm tra phương pháp mới không tệ hơn baseline quá một biên đã chọn trước |
| p95/p99 | Mức mà 95%/99% quan sát không vượt; dùng nhìn đuôi xấu |
| Gini | Chỉ số độ bất đều phân phối, 0 là đều hơn; không tự động là fairness nhóm |
| VHT/VMT | Tổng giờ xe chạy/tổng quãng đường xe chạy |
| Service rate | Tỷ lệ request được accept/hoàn tất theo định nghĩa công khai |
| Event sourcing | Lưu chuỗi event để dựng lại trạng thái |
| Idempotency | Gửi lại cùng event không làm áp dụng hai lần |
| Canonical JSON | Cách serialize JSON cố định để hash/cross-language đồng nhất |
| NDJSON/JSONL | Mỗi dòng là một JSON object, phù hợp stream |
| Adapter | Lớp chuyển đổi giữa simulator/product và contract chung |
| Portable core | Một lõi dùng lại thật sự ở nhiều môi trường, không viết lại |
| Capability matrix | Bảng nói rõ framework hỗ trợ/không hỗ trợ chức năng nào |
| Preflight | Kiểm tra nhỏ trước khi đầu tư tích hợp/chạy lớn |
| Pilot | Thí nghiệm thử để đo variance/runtime và khóa thiết kế |
| Preregistration | Ghi trước giả thuyết, data, metric và cách phân tích trước full run |
| Confirmatory | Phần thí nghiệm dùng kiểm chứng giả thuyết đã đăng ký |
| Exploratory | Phân tích tìm hiểu thêm, không được trình bày như confirmatory |
| External validity | Mức kết quả có thể áp ra môi trường khác; simulator khác chưa đủ chứng minh thực tế |
| Incident | Sự cố như hỏng xe/đóng đường cho phép recovery đặc biệt có audit |

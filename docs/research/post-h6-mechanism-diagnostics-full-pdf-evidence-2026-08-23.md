# Post-H6 mechanism diagnostics — full-PDF evidence

> Trạng thái: `EVIDENCE_READ_COMPLETE`
> Ngày: 2026-08-23
> Phạm vi: thiết kế exploratory hậu H6; không sửa preregistration hoặc verdict WP9

## 1. Câu hỏi thiết kế

H6 cho thấy C1 giảm tỷ lệ hoàn thành ở cả hai panel. Tài liệu này chỉ trả lời câu
hỏi tiếp theo: bằng chứng hiện có cho phép định vị cơ chế mất dịch vụ đến đâu, và
phải đo thêm gì trước khi thiết kế commitment policy v2?

Không tài liệu nào dưới đây được dùng để đổi margin, panel, denominator, failed-job
treatment hoặc biến một phân tích hậu outcome thành confirmatory result.

## 2. Full PDF đã đọc

Ba PDF được mở bằng in-app Browser để xác nhận đúng full document, sau đó kiểm tra
SHA-256, trích xuất và đọc tuần tự mọi trang. Bản cục bộ được giữ ngoài repository
tại `E:\RideBoundData\research\pdf-20260823-post-h6`.

| Tài liệu | Trang đã đọc | SHA-256 | Nguồn full PDF |
|---|---:|---|---|
| Pillac, Gendreau, Guéret, Medaglia, *A Review of Dynamic Vehicle Routing Problems* | 29/29 | `770027591d40b271e3a2832b6cc9c4234220e8fa218fe747d04b7f2fe27f739d` | `https://www.cirrelt.ca/documentstravail/cirrelt-2011-62.pdf` |
| Ulmer, Goodson, Mattfeld, Thomas, *On Modeling Stochastic Dynamic Vehicle Routing Problems* | 42/42 | `5bc8131bf4d5bb1711a6658486e1f256e9ddf504f2388709696ec2af17762695` | `https://justin-goodson.com/papers/RouteBasedMDPs.pdf` |
| Ackermann, Rieck et al., *Multiple plan approach for a dynamic dial-a-ride problem* | 35/35 | `059a0bffc546e8588f1d9487ddb16344e7c24c559bc54dfa104979f98e223d3c` | `https://link.springer.com/content/pdf/10.1007/s00291-025-00809-y.pdf` |

Tổng cộng 106/106 trang có text; trang đầu/giữa/cuối của từng PDF đã render và
được kiểm tra trực quan. `fulltext-inventory.json`, text theo trang và ảnh render
nằm cùng corpus. Hai HTML client-challenge thất bại được giữ như receipt; chúng
không được tính là full-text evidence.

## 3. Kết luận được áp dụng

### 3.1 State, action và trajectory phải tách rời

Khung route-based MDP của Ulmer et al. tách pre-decision state, action/route plan,
post-decision state, thông tin ngoại sinh và state kế tiếp. Vì vậy WP13 phải tách:

- khác biệt quyết định trên cùng lịch sử input quan sát được;
- mất mát xuất hiện ngay tại quyết định đó;
- kết quả downstream sau khi hai trajectory đã khác.

Loại thứ ba chỉ được gọi là `trajectory-associated`; không được gọi là causal effect
của một witness riêng lẻ. `ForwardSlackProfile` tiếp tục là chứng nhận pruning cục
bộ, không phải hàm giá trị tương lai.

### 3.2 Service, diversion và reactiveness là các trục khác nhau

Pillac et al. phân biệt acceptance/service guarantee, khả năng diversion, tần suất
và urgency của thông tin động, cùng đánh đổi giữa computation time và reactiveness.
Do đó WP13 không được diễn giải burden gần zero do lock/từ chối thành tối ưu hóa
revision. Báo cáo phải giữ completed service và burden cạnh nhau, đồng thời ghi rõ
phần burden giảm theo định nghĩa cơ chế.

### 3.3 Multiple-plan và slack không có default phổ quát

Ackermann et al. cho thấy hiệu quả của plan pool, secondary objective, waiting policy
và insertion procedure tương tác mạnh với cấu hình. Trong thí nghiệm của họ, node
slack/double horizon có thể kém hơn distance và tăng thời gian không bảo đảm tăng
service. Vì vậy:

- không tăng `30 s` budget một cách cơ học;
- không thay objective bằng slack hoặc consensus từ paper;
- option-set/plan-pool chỉ là exploratory diagnostic: số candidate hard-valid,
  coverage theo vehicle/request và compatibility nếu Runner vNext ghi đủ evidence;
- mọi horizon, relaxation hoặc secondary objective mới phải được derive từ evidence
  phát triển và freeze trước H7.

## 4. Phần bị từ chối hoặc hoãn

- Không dùng abstract/snippet của Mitrović-Minić hoặc Bent–Van Hentenryck vì chưa
  lấy được full PDF đáng tin cậy trong phiên này.
- Không đưa scenario sampling, demand forecasting, ADP hoặc MSA vào WP13; H6 không
  có transition/value evidence để đánh giá chúng công bằng.
- Không coi candidate pool lớn hơn là tốt hơn nếu chưa đo compatibility và service.
- Không copy threshold, runtime budget, horizon hoặc distribution tổng hợp từ paper.
- Không dùng rerun exploratory để sửa tên, hash hoặc outcome của H6.

## 5. Hệ quả cho delivery

WP13 bắt đầu bằng evidence-sufficiency inventory và arm-independent observed-history
alignment. Nếu H6 không chứa full retained-candidate route/schedule, metric cần dữ
liệu đó phải được ghi bởi Runner evidence version mới và chạy dưới exploratory freeze
mới. Chỉ sau independent verifier/mutation gates mới mở WP14 ablation/Pareto.


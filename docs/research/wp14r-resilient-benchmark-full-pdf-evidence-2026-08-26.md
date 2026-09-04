# WP14R — bằng chứng full PDF cho benchmark có khả năng phục hồi

> Ticket: `RB-WP14R-001`
> Ngày kiểm tra: 2026-08-26
> Phạm vi: phương pháp benchmark và độ tin cậy cơ học; không phân tích outcome WP14

## 1. Câu hỏi dẫn đường

WP14 freeze v1 đã dừng đúng fail-closed khi B1 hoàn tất còn C1 chỉ có partial
transcript. Câu hỏi mới không phải là “làm thế nào để chạy lại C1”, mà là:

1. một successor phải tách variation nào trước khi benchmark;
2. phải ghi gì để một lần launcher bị chấm dứt vẫn có evidence kiểm toán được;
3. recovery nào được khai báo trước mà không biến retry thành experimental unit hoặc
   chọn kết quả thuận lợi;
4. bằng chứng nào chỉ hỗ trợ reliability/performance methodology và không được dùng
   để cứu result WP14-v1.

## 2. Corpus và kiểm tra toàn văn

| Nguồn | Toàn văn đã đọc | SHA-256 bản lưu cục bộ | Provenance |
|---|---:|---|---|
| Tomas Kalibera & Richard Jones, *Rigorous Benchmarking in Reasonable Time*, ISMM 2013 | 12/12 trang | `b50fb85079cbaea9524eb202393a60807dd3fc270d91eb80de5dba0faf02dbab` | [Kent Academic Repository PDF](https://kar.kent.ac.uk/33611/45/p63-kaliber.pdf), đối chiếu [trang tác giả tại University of Kent](https://www.cs.kent.ac.uk/people/staff/rej/papers/) |
| Todd Mytkowicz et al., *Producing Wrong Data Without Doing Anything Obviously Wrong!*, ASPLOS 2009 | 12/12 trang | `67505bfc1f5a9a442d3ba7f5a5a22e05e55569237def1964a7eab2e7533ee2d6` | [University-hosted PDF](https://eecs481.org/readings/producing-wrong-data.pdf), đối chiếu [trang publication của nhóm tác giả](https://sape.inf.usi.ch/publications/asplos09.html) |

Hai PDF được mở qua in-app Browser trước khi tải vào corpus ngoài repository ở
`E:\RideBoundData\research\pdf-20260826-wp14r-benchmark-methodology`. Sau đó toàn bộ
24/24 trang được extract text, render bằng Poppler và kiểm tra contact sheet cùng các
trang đại diện ở kích thước đầy đủ. Không dùng abstract hoặc chỉ tiêu đề làm evidence.

Ghi chú provenance: endpoint Kent dùng một intermediary certificate tự ký đối với
CLI trên host này. Browser vẫn hiển thị đúng PDF và trang tác giả xác nhận publication;
bản lưu CLI được lấy sau bước xác nhận đó với TLS verification tắt có chủ ý. SHA-256
ở trên là identity của bytes thực tế đã đọc, không phải xác nhận publisher signature.

## 3. Điều học được và ánh xạ vào RideBound

### 3.1 Kalibera & Jones

Paper tách các nguồn variation theo level thí nghiệm, ví dụ build/VM, process và
iteration. Repetition phải đặt ở level cao nhất còn variation; số lần lặp không được
sao chép từ hệ khác mà phải dimension theo benchmark/platform đang xét. Paper cũng
yêu cầu báo effect size cùng uncertainty phù hợp khi đưa ra performance claim.

Áp dụng cho successor:

- inventory phải phân biệt host session, launcher process, simulator job và bước
  verifier; một seed solver đã bất biến không tự trở thành replicate;
- `RB-WP14R-005` chỉ dimension repetition sau khi telemetry và fault mechanics đã
  ổn định, không dùng một B1 observation để pass paired envelope;
- mọi speed/resource claim tương lai phải nói rõ level variation và estimand. Không
  nhập số repetition của paper thành default RideBound.

Không áp dụng cho WP14-v1:

- không tạo CI hay population inference từ finite development panel;
- không dùng lý thuyết repetition để chạy lại observation đã bị freeze cấm retry;
- không biến recovery attempt thành independent unit.

### 3.2 Mytkowicz et al.

Paper chứng minh các thay đổi setup tưởng như vô hại có thể đảo chiều kết luận; chạy
lặp cùng một setup không loại được systematic setup bias. Hai hướng được đề nghị là
randomize/diversify setup đã biết và dùng causal intervention để kiểm tra nguồn bias.

Áp dụng cho successor:

- ghi exact command binding, host/process metadata và process-tree terminal state;
- fault injection là intervention cơ học: kill child/launcher ở các boundary đã khai
  báo rồi kiểm tra ledger, retained partial và quyết định recovery;
- nếu nghiên cứu setup variation ở `RB-WP14R-005`, thứ tự/setup phải predeclare và
  không đọc scientific outcome để quyết định lần chạy tiếp theo.

Không áp dụng:

- không randomize policy, denominator hoặc factor sau khi nhìn outcome;
- không gọi fault-injection pass là effectiveness evidence;
- không tuyên bố causal mechanism của service result từ operational failure.

## 4. Audit trực tiếp failure boundary WP14-v1

`simulators/fleetpy-ridebound/wp14_run_matrix.py` gọi simulator bằng
`subprocess.run(..., capture_output=True, ...)`. Job log chỉ được exclusive-write sau
khi child trả về hoặc đi qua `TimeoutExpired` do runner kiểm soát. Nếu outer launcher
bị chấm dứt, stdout/stderr đang ở bộ nhớ của launcher mất theo process; partial output
có thể còn nhưng job log và run summary không được phát hành. Lần chạy kế tiếp thấy
output tồn tại, verify bundle fail vì thiếu manifest, rồi ghi failure summary. Đây là
giải thích source-level cho hình dạng evidence, không xác định được tác nhân đã chấm
dứt session cụ thể.

File runner đó nằm trong freeze receipt v1 và **không được sửa**. Mọi mechanics mới
phải dùng namespace/tool/schema `wp14r` và không được diễn giải lại ADR-070.

## 5. Quyết định thiết kế

1. Mở `WP14R` như resilient-execution successor riêng; chưa mở WP15.
2. Giữ WP14-v1 terminal FAIL CLOSED và toàn bộ partial bytes.
3. Recovery v2 được khai báo cố định là một initial attempt cộng tối đa một recovery
   attempt. Tất cả attempt retained, immutable, không overwrite.
4. Recovery chỉ dựa trên mechanical validity, không đọc completed/burden/route outcome.
5. Không retry sau bundle đã independently verify pass; attempt thứ ba bị cấm.
6. `RB-WP14R-002` chỉ xây ledger/state machine. Supervision, incremental logging,
   heartbeat, process tree và fault injection là ticket sau.
7. Chỉ một protocol/ADR freeze v2 sau các gate cơ học mới có thể authorize paired
   resource gate/matrix mới; nó không thay thế freeze receipt v1.

## 6. Claim boundary

Corpus này hỗ trợ thiết kế measurement/reliability. Nó không hỗ trợ kết luận rằng
RideBound tốt hơn B1, không cứu service gate, không chứng minh SLA/cross-city validity,
và không cho phép lấy retry tốt nhất. Mọi scientific outcome của successor vẫn phải
đi qua protocol pre-outcome và independent verifier riêng.

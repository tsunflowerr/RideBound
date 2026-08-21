# Pre-outcome amendment: uniform request target 108

> Ngày 2026-08-21. Amendment cơ học trước khi bất kỳ Runner nào được gọi trên
> holdout. Preregistration gốc giữ nguyên bytes/hash.

Materialization grid v1 dừng fail-closed ở `2018-11-17/sample_10_3`: selection
key đã khóa chỉ có 125 request nằm trọn trong node pool 96, nhỏ hơn target 128.
12 derivative trước điểm lỗi chỉ là artifact materialization, không có outcome.

Thay đổi duy nhất cho grid v2 là đặt `requestTarget=108` đồng nhất trên cả 20
cell. Không đổi ngày, sample, selection/pseudonymization label, cửa sổ, fleet,
capacity, time window, travel factor, policy hay metric.

108 là target lớn nhất khả dụng đồng nhất dưới node cap 96: kiểm cơ học trên
demand endpoint/node-pool selection đã khóa cho min theo ngày lần lượt là
165/145/121/108; cell nhỏ nhất là `2018-11-18/sample_10_3`. Phép kiểm chỉ đọc
source coverage và HMAC selection, không chạy policy/Runner và không đọc outcome.

Node cap không được nới vì nó là bound v1 của normalizer và complete directed
snapshot. Không loại riêng cell lỗi vì như vậy sẽ đổi panel theo feasibility hậu
nghiệm. Cổng service vẫn dùng exact denominator quan sát bằng nhau; expected
denominator mới là 20 × 108 = 2.160 mỗi arm.

Execution freeze phải bind cả hash preregistration gốc và hash amendment này.

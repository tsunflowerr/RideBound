# Leakage và adversarial audit — RB-WP8-013

## Kết luận

Trước freeze không có bundle/output policy cho ngày `2018-11-14..18`. Việc đọc
holdout trước WP9 giới hạn ở header/schema, demand endpoint coverage và deterministic
normalization; không có completed/burden/rejection/solver outcome.

Selection key được dẫn xuất từ preregistration hash. V1 materialization failure
được xử lý đồng nhất toàn panel bằng amendment target 108; không chọn bỏ cell và
không thay key. V1 partial derivative được giữ, dán nhãn không dùng.

## Đường rò rỉ và cổng

| Đường rò rỉ/sai lệch | Cổng fail-closed |
|---|---|
| đổi source/grid/selection label | derivative/config/scenario hash đổi; freeze receipt lệch |
| thay driver scenario hash/count | preflight từ chối trước Runner |
| đổi config/policy/budget | configuration binding và bundle inventory lệch |
| đổi repo trong run | source inventory trước/sau khác, bundle fail |
| tráo arm hoặc demand | typed pairing contract từ chối |
| giả N bằng solver seed | seed không thuộc experimental-unit identity; plan test khóa 20 units |
| omission/saturation/fallback | audited solver verifier từ chối |
| lật transcript/checkpoint/report byte | bundle/hash-chain/semantic verifier từ chối |
| burden tốt nhưng service xấu | exact gate test bắt `gateFailed` |

Python mutation/contract suite hiện hành 77/77 gồm verifier, plan, freeze và analysis
gate. Oracle
burden chạy ngoài tiến trình và có mutation tạo bất đồng. Old WP7 verifier output
giữ byte-identical ở chế độ mặc định.

Không có đường nào tự chứng minh external validity. Freeze chỉ loại researcher
degrees of freedom trong panel đã khai; forbidden claims vẫn áp dụng.

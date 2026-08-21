# Pre-outcome amendment: Runner artifact repin — WP8-011c

> Thời điểm: 2026-08-21, trước khi chạy bất kỳ job Layer-2/WP9
> confirmatory nào. Chưa có confirmatory outcome khi amendment này được lập.

## Phát hiện provenance

Audited Layer-1 mechanical harness chạy từ build Release hiện hành và PASS 8/8.
Kiểm tra receipt sau đó phát hiện Runner DLL mà harness thật sự gọi có SHA-256
`4c297a2cdaf7a915200d3069552b0325375b04a32ac70acdf3aff9ea3adbd2a8`, còn
freeze receipt v1/v2 ghi artifact cũ
`16f3b5e8d4d774a0c35f6738ac95a4c0afab30e556fc8db97609ae26c5393aad`.
Hai file cùng 174.592 byte nhưng không byte-identical.

Đây là lỗi pin/provenance được phát hiện trước outcome confirmatory, không phải
tín hiệu hiệu quả. Layer-1 đã tồn tại tại thời điểm phát hiện nhưng chỉ là bằng
chứng mechanical, không thuộc fixed-panel estimand và không được dùng để thay đổi
endpoint, margin, panel hay policy.

## Repin fail-closed

Một publish Release sạch từ source hiện hành được materialize ngoài repository tại
`E:\RideBoundData\wp9\runner\audited-evidence-hotpath-v2-current`. Runner DLL của
publish này byte-identical với DLL Layer-1 đã gọi (`4c297a2c…bd2a8`). Toàn bộ 19
file trong publish tree cũng byte-identical, theo từng tên và SHA-256, với Runner
tree mà Layer-1 đã dùng.

Freeze receipt kế tiếp phải:

- supersede receipt v2 và bind amendment này;
- pin cả Runner DLL lẫn tree seal của toàn publish root, không chỉ một assembly;
- bind verifier mới và loại rõ ba receipt lịch sử khỏi scenario-plan tree seal;
- được verifier recompute PASS trước audited smoke.

## Những gì không đổi

Không đổi preregistration payload, node-cap amendment, analysis-integrity
amendment, 20-cell panel, 108 arrivals/run, 40 primary + 20 robustness jobs,
scenario derivatives, configs, solver seeds, endpoint, strict margin `1,0 pp`,
missingness rule, analysis manifest hay negative-result policy. Repin chỉ làm cho
artifact thực thi khớp source đã review và bằng chứng Layer-1 đã thu.

Bất kỳ thay đổi outcome-bearing nào sau smoke vẫn invalidate affected run theo
preregistration; amendment này không cho phép vá sau khi xem kết quả.

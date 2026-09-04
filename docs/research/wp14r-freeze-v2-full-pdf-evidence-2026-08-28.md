# WP14R — full-PDF evidence cho freeze-v2 và host conditioning

> Ticket: `RB-WP14R-007`  
> Ngày kiểm tra: 2026-08-28 (Asia/Bangkok)  
> Phạm vi: setup bias, host conditioning và protocol authorization; không đọc
> scientific outcome WP14R

## 1. Câu hỏi dẫn đường

Sau khi mechanics `RB-WP14R-002..006` pass, câu hỏi còn lại không phải chỉ là
“supervisor có sống sót qua crash không”. Freeze v2 còn phải trả lời:

1. chạy lặp trên cùng một setup có đủ loại systematic bias hay không;
2. power source, power scheme và host load nào phải khóa trước launch;
3. recovery attempt giữ command identity thế nào khi output của hai attempt phải tách;
4. phần nào của một paper về runtime layout có thể áp dụng cho mô phỏng RideBound, và
   phần nào phải từ chối.

## 2. Corpus mới và kiểm tra toàn văn

| Nguồn | Toàn văn đã đọc | SHA-256 bản lưu cục bộ | Provenance |
|---|---:|---|---|
| Charlie Curtsinger & Emery D. Berger, *Stabilizer: Statistically Sound Performance Evaluation*, ASPLOS 2013 | 10/10 trang | `819c930cc8f51a65a24cdc46452a29ec2c872391724974d21589a8729dac9c49` | [PDF chính thức tại UMass Amherst](https://people.cs.umass.edu/~emery/pubs/stabilizer-asplos13.pdf) |

PDF được mở và xác nhận URL/title trong in-app Browser, sau đó lưu ngoài repository:

```text
E:\RideBoundData\research\pdf-20260828-wp14r-freeze-v2-methodology\curtsinger-berger-2013-stabilizer.pdf
```

Toàn bộ 10/10 trang có text không rỗng, tổng text extract 64.079 byte. Mười trang được
render ở 110 DPI; contact sheet cùng trang 1, 6 và 9 được kiểm tra ở kích thước đầy đủ.
Các khung xanh/đỏ nhìn thấy trong render là annotation hyperlink của PDF, không phải
lỗi mất chữ. Không dùng abstract, snippet tìm kiếm hoặc chỉ tiêu đề làm evidence.

Freeze v2 đồng thời bind lại hai full PDF đã đọc ở `RB-WP14R-001`:

- Kalibera & Jones 2013, 12/12 trang, SHA `b50fb850…dbab`;
- Mytkowicz et al. 2009, 12/12 trang, SHA `67505bfc…2d6`.

Do đó methodology identity trong receipt gồm đúng 34/34 trang và ba SHA đầy đủ.

## 3. Điều học được từ Stabilizer

Paper chỉ ra một executable/setup cụ thể vẫn là một sample của không gian layout;
chạy lại chính binary đó nhiều lần không làm systematic layout bias biến mất. Những
yếu tố môi trường không được randomize hoặc kiểm soát vẫn có thể lấn át effect đang
đo. Stabilizer giải quyết một lớp bias runtime bằng cách randomize layout trong lúc
chạy, rồi dùng thiết kế thống kê phù hợp cho performance experiment của họ.

Ánh xạ hợp lệ vào RideBound:

- repetition của cùng process/setup không được gọi là independent experimental unit;
- host session, power source/scheme và pre-launch load là các conditioning factor phải
  ghi và kiểm tra, không được coi quiescent theo cảm giác;
- command, environment, working directory và executable phải bind bằng hash;
- setup thay đổi sau freeze phải làm verification fail, không được im lặng nhập vào
  cùng result set.

Không áp dụng:

- RideBound không dùng Stabilizer và không claim đã randomize code/stack/heap layout;
- không copy số run, ANOVA recipe hoặc numeric threshold của paper;
- paper đo runtime performance, còn WP14R có scientific endpoint về service/burden và
  một resource gate riêng. Không suy rộng statistical conclusion của paper sang panel;
- không tạo CI/population claim cho finite 16-cell development matrix;
- không dùng setup randomization để đổi arm, factor, denominator hoặc cứu H6/WP14-v1.

## 4. Bằng chứng local làm lộ setup factor

Trong revalidation 2026-08-28, exact .NET medium public-drain test fail CPU gate hai
lần khi build server còn sống. Sau `dotnet build-server shutdown`, exact test 1/1 và
full `dotnet test RideBound.slnx` 908/908 pass mà không đổi source hay trần 120 giây.
Read-only diagnostic cùng session ghi Windows `Balanced`, battery discharging và clock
hiện hành thấp hơn maximum. Đây không chứng minh causal contribution riêng của từng
factor, nhưng đủ bác bỏ giả định “cùng source thì host setup không cần bind”.

## 5. Quyết định áp dụng vào freeze v2

Receipt `freeze-v2-authorization.json` khóa trước outcome:

1. đúng Windows host fingerprint và pinned Python/FleetPy/Runner identity;
2. bắt buộc AC online; battery không được launch;
3. active power scheme phải đúng GUID Balanced
   `381b4222-f694-41f0-9685-ff5bb260df2e`;
4. đúng 10 CPU interval, mỗi interval 1 giây; mean không quá 20% và một sample không
   quá 60%; thresholds này là conservative local engineering gate, không lấy từ paper;
5. available memory ít nhất 8 GiB, free disk trước launch ít nhất 25 GiB;
6. không ghi arbitrary process name hay command line vào receipt;
7. execution tuần tự `maximumParallelJobs=1`; pair B1 rồi C1 là exact hai job cũ,
   không đổi scientific design;
8. attempt 1/2 dùng cùng wrapper command. Wrapper suy ra `attempt-XX/output` từ
   immutable open ledger, nên output tách mà `commandSha256` recovery không đổi;
9. paired gate chỉ đọc ledger/resource inventory, không đọc completed/burden; matrix
   chỉ mở nếu hai job valid, zero failed và resource envelope pass;
10. mọi source/schema/test của orchestrator cùng evidence gate `002..006` được hash
    trong receipt. WP14-v1 receipt vẫn được read-only reverify byte-exact.

Các rule trên conditioning cho exact host này; chúng không tạo between-host claim và
không nói Balanced tốt hơn power scheme khác. Nếu cần host khác, phải có freeze mới
trước outcome, không trộn kết quả.

## 6. Kết quả preflight authorization

Preflight cơ học thật được chạy sau khi ký receipt, không launch B1/C1:

| Trường | Quan sát |
|---|---:|
| Power source | `offline` |
| Power scheme | exact Balanced GUID |
| CPU samples | 10/10; range `8.538%..14.656%` |
| Mean CPU | `11.790%` |
| Available memory | `8.861.749.248` byte |
| Free disk | `144.954.671.104` byte |
| Typed decision | `FAIL — POWER_SOURCE_NOT_AC` |

Retained receipt:

```text
E:\RideBoundData\wp14r\development-v2-control\authorization-preflight-20260828.json
SHA-256: 642b23efaf107e1e8ea99b68494dc3b5b0b6b7fab363861701b38eecc06cd622
```

Failure này là expected environment block, không tiêu thụ attempt và không phải
scientific result. `RB-WP14R-008` chỉ được launch khi một preflight mới pass toàn bộ
frozen conditions; không tự đổi power scheme và không hạ threshold để lấy pass.

## 7. Claim boundary

Corpus và preflight này hỗ trợ một within-host measurement protocol chặt hơn. Chúng
không chứng minh RideBound hiệu quả hơn B1, không tạo speedup claim, không tăng cỡ mẫu,
không rescue H6/WP14-v1, và không biến recovery attempt thành replicate.

# WP9 main experiment — ordered ticket plan

> Nguồn khóa: preregistration `H0=c653c3ce…b1fc2`, amendment
> `184bf923…fddf9`, analysis-integrity amendment `db1731e6…f1d70`,
> Runner-repin amendment `d1d9465e…ac90e`, capacity-stratum amendment
> `wp8-011d` (2026-08-22), historical receipts
> `H2=97af95cf…d3049`/`H3=d028eae4…dd14e`, current receipt
> `H4=2f7e6bf3…a32dd`.
>
> `H4` đã hết hiệu lực vận hành sau ADR-045 và `wp8-011d`: cả hai đều
> outcome-bearing. `RB-WP9-002a` phải repin thành `H5` trước audited smoke, và
> mọi run trước `H5` không được trộn vào estimand.

| Ticket | Nội dung | Điều kiện đóng |
|---|---|---|
| `RB-WP9-001` **Done** | Enrollment/freeze seal | 20 units, 40 primary + 20 robustness jobs; H4 verifier recompute 25 files + derivative/scenario/Runner tree seals PASS; no confirmatory outcome |
| `RB-WP9-002a` **Ready** | Repin freeze sau ADR-045 + `wp8-011d` | `H5` bind Runner build mới, execution plan/grid hai stratum, hai amendment; verifier recompute PASS |
| `RB-WP9-002b` | Pinned Runner và audited smoke | Runner SHA exact; một pair mỗi stratum qua independent verifier với solver evidence; smoke phải chạy qua một epoch có exogenous breach thật |
| `RB-WP9-002c` | Bắc cầu breach vào ledger cam kết | `ExogenousServiceQualityBreach` → `CommitmentBreachRecord`/`AppendBreach` trên đường quyết định; đối xứng hai arm; regression khoá |
| `RB-WP9-003` **Done** | Layer-1 mechanical paired evidence | 8/8 PASS, external verify; current Runner DLL/publish tree byte-identical; không nâng thành Layer-2 effectiveness |
| `RB-WP9-004` | Chạy full Layer-2 fixed panel | 20 cell × 2 arm × 2 stratum = 80 primary + 20 robustness; terminal receipt cho từng job; repo inventory bất biến trong mỗi run |
| `RB-WP9-005` | Exact panel analysis | 20 paired rows **mỗi stratum**, aggregate burden/service gate riêng từng stratum, locked/earned split, độ chính xác đạt được (~1,40 pp) in cạnh mọi gate; canonical output |
| `RB-WP9-006` | Robustness/ablation | C1 unbounded, C2, seed19 subset, chỉ ở stratum `veh8`; không tăng N/không cứu primary |
| `RB-WP9-007` | Reproducibility bundle | verifier độc lập 100/100; identity chuỗi demand+travel PASS bốn điều kiện của `wp8-010`; mutation; hashes/plan/analysis/raw artifact |
| `RB-WP9-008` | Source/claim closure | review logic, ADR/status/traceability, negative result giữ nguyên, WP9 verdict **có điều kiện theo năng lực** |

Không ticket nào được sửa prereg payload, bốn amendment, grid, execution plan,
analysis/robustness manifest hoặc Runner artifact đã repin. Program được bind bởi
`H5`; bug ảnh hưởng outcome sau smoke phải invalidate affected grid theo
preregistration, không vá lẻ.

## Ràng buộc claim đã khóa trước outcome

- Kết luận là **có điều kiện theo năng lực**: chi phí dịch vụ của cam kết bằng 0 ở
  năng lực dư và dương thật ở năng lực căng, phát biểu tại đúng hai điểm đã đo.
  Luận điểm phổ quát "cơ chế cam kết rẻ" bị cấm kể cả khi `veh8` đạt gate.
- Chỉ kết luận trên finite panel; không population p-value/NI CI. Sàn sign-flip là
  `1/2^5 = 0,03125` vì panel chỉ có 5 travel realization.
- Exogenous burden là diagnostic mô tả, **không** phải negative control
  (`wp8-010`). Falsification dùng identity chuỗi demand+travel.
- Phần pickup-ETA của C1 là definitional theo lock; tỷ lệ pilot 18%/82% không
  được mặc định cho confirmatory.

"""Record review findings and next actions; retain deployed thesis unchanged."""
from pathlib import Path
import json

docs = Path('C:/Code/RideBound-noworse/docs')
resume = Path('C:/RideBoundContinuation/2026-10-15/RESUME-STATUS.md')
audit = 'C:/RideBoundContinuation/2026-10-16/thesis-readiness-audit/THESIS-READINESS-AUDIT.md'
note = f'''## Rà soát mức sẵn sàng bảo vệ — 16/10/2026

Theo yêu cầu người dùng, đã truy vết bản PDF77trang hiện tại tới nguồn LaTeX,
mã chuẩn hóa/dự báo/thống kê và đầu ra đã giữ.22tệp payload và13mục manifest
kết quả khớp; số gộp bốn cấu hình khớp; hash5tệp chuẩn hóa hiện tại trùng
34517a83…4d90của49report, mỗi report108yêu cầu. Không sửa PDF/nguồn luận văn,
core, frozen inputs, code phân tích, output cũ, env, Git hay lịch tự động.

**Trạng thái:** bản thảo đầy đủ và nghiên cứu bổ sung đã xong; chưa đóng
“toàn bộ khóa luận/bản nộp đã hoàn tất”. Review chỉ ra việc cần hoàn thiện:
R01công khai60tổ hợp→49dựng được→34test/15hiệu chỉnh và11tổ hợp bị loại trước
mô phỏng do giới hạn96nút/quy tắc chuẩn hóa; R02khoảng95% theo8nhóm ngày–khung
còn phụ thuộc chéo và chỉ3ngày bật dự báo; R03cần phân biệt lớp lời hứa phát
lại với tích hợp dự báo vào điều phối (29/32hoàn tất,3dừng;34/1471lời hứa đón
hiển thị vượt hạn); R04có chênh với hướng dẫn UET công khai, cần xác nhận mẫu
áp dụng trước chỉnh hình thức. Không coi mọi hạn chế là lỗi thuật toán.

Chưa có đối chứng trực tiếp cộng dồn-vs-từng-lần, xác nhận mới trên ngày chưa
xem, lợi nhuận/realizedcost, SLAđộ trễ mỗi quyết định hoặc artifact tái lập từ
máy sạch được xác nhận trong lượt này. Đây là giới hạn của mức claim, không
tự cấp quyền chạy nghiên cứu mới. H6/E1/cross-system cũ giữ đúng verdict;
không nâng WP12, không Stage2. Baseline.NET1.033/1.034với1knownfailure giữ nguyên.

Next: bổ sung mô tả chọn mẫu; hạ/giải thích suy luận thống kê; ma trận trạng
thái thành phần và phạm vi đóng góp; chốt mẫu áp dụng với GVHD/khoa; recipe,
slide và demo đúng phiên bản. Chi tiết13điểm, câu hỏi bảo vệ và thứ tự xử lý:
[báo cáo rà soát]({audit}).
'''

changes = []
p = docs/'18-status-and-decision-log.md'
s = p.read_text(encoding='utf-8')
old = next(x for x in s.splitlines() if x.startswith('> Tiếp phiên ngoài cây chính:'))
s = s.replace(old, '> Tiếp phiên ngoài cây chính: 2026-10-16 — ADR-078; nghiên cứu bổ sung hoàn tất, bản luận văn77trang đã giao. Audit mức sẵn sàng bảo vệ còn mở R01chọn mẫu/R02thống kê/R03phạm vi tích hợp/R04mẫu UET; chưa đóng bản nộp hoặc WP12. Hai lịch PAUSED.', 1)
needle = '**Consequences:** Không đổi implementation/protocol, baseline/panel, H6/E1/'
assert s.count(needle)==1
s = s.replace(needle,note+'\n'+needle,1)
changes.append((p,s))

p=docs/'19-requirement-traceability.md'
s=p.read_text(encoding='utf-8')
needle='\n## 1. Yêu cầu từ mục tiêu dự án'
assert s.count(needle)==1
row=f'| Rà mức sẵn sàng bảo vệ, không đồng nhất PDF với hoàn tất nghiên cứu | [Audit13điểm]({audit});trace-evidence/selection-trace;22payload+13result entries khớp | Còn mô tả60→49→34/15, giả định CI, trạng thái dự báo và mẫu áp dụng;chưa đóng bản nộp/WP12;không chạy nghiên cứu mới |\n'
s=s.replace(needle,row+needle,1)
changes.append((p,s))

p=docs/'benchmarking/thesis-stage1b-continuation-2026-10-15.md'
s=p.read_text(encoding='utf-8')+'\n\n'+note
changes.append((p,s))
s=resume.read_text(encoding='utf-8')
needle='> Không rerun/overwrite study outputs, sửa frozen files hoặc commit/push. Các mốc cũ bên dưới là lịch sử.'
assert s.count(needle)==1
s=s.replace(needle,needle+'\n> Audit mới16/10: bản thảo đầy đủ, nhưng bản nộp chưa đóng; cần xử lý chọn mẫu, suy luận thống kê, trạng thái tích hợp dự báo và mẫu UET. Xem closure rà soát cuối.',1)
s+='\n\n'+note
changes.append((resume,s))
for path, text in changes:
    path.write_text(text,encoding='utf-8')
print(json.dumps(dict(updated=[str(p) for p,_ in changes],audit=audit),ensure_ascii=False,indent=2))

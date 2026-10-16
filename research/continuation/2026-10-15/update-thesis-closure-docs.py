"""Record completed thesis delivery without changing experimental conclusions."""
import datetime
import json
from pathlib import Path

ROOT = Path('C:/RideBoundContinuation/2026-10-15')
DOCS = Path('C:/Code/RideBound-noworse/docs')
r = json.loads((ROOT / 'thesis-v5-deployment.json').read_text(encoding='utf-8'))
when = r['created']
day = when[:10]
pages, digest, backup = r['pages'], r['pdfSha256'], r['backup'].replace('\\', '/')
details = f'''## Cập nhật khóa luận — {when}

Người dùng yêu cầu cập nhật toàn bộ nội dung theo cách đọc tự nhiên, bổ sung
đánh đổi giữa hai thứ tự chọn phương án và giải thích lý do cần phép so bổ sung.
Đây là quyền cập nhật bản luận văn thật, thay trạng thái chờ duyệt trước đó.

Đã sao lưu nguyên thư mục luận văn cũ tại `{backup}` (có manifest từng tệp),
rồi cập nhật đúng `E:/Code/Report_INT3508/ridebound/`. Bản PDF cuối **{pages} trang**,
SHA-256 `{digest}`. Bộ nguồn, logo, README và hồ sơ kiểm đi kèm khớp bản đã kiểm.
Biên nhận: [thesis-v5-deployment.json](C:/RideBoundContinuation/2026-10-15/thesis-v5-deployment.json).

Đã đồng bộ tóm tắt Việt/Anh, Chương 1, 3, 4, 5 và phụ lục. Dùng “ưu tiên ổn
định” và “ưu tiên chi phí kế hoạch”, bỏ cách gọi bộ chọn cũ/mới và tên giai đoạn
nội bộ khỏi nội dung PDF. Bảng bốn cấu hình giữ cả served/shared, trễ, đón muộn,
khách bị sửa giờ và sửa lặp. Nêu rõ +26 served/+119 shared cùng C30 đổi lại
33→106 khách bị sửa giờ, 0→2 bị sửa lặp, 43→44 đón muộn, 10→11 forecast late60.
Phép so kiểm xem giới hạn còn giữ được khi bỏ xếp hạng ổn định; không kết luận
selector ban đầu kém hơn, không nhận pure causal isolation hay trần30 tối ưu.

Giữ đối chứng hạn chót/đệm bất lợi, hạn chế dự báo, H6/E1 và điểm dừng H7.
Phân biệt bảy lịch sử ở trần giả định120 với một cumulative-only prune C30;
không suy thành outcome advantage của cộng dồn so per-step. Kế hoạch chạy thêm
bốn cấu hình không còn bị ghi là chưa làm/hướng tương lai.

Tectonic exit0; không Overfull, undefined/duplicate references hoặc TeX errors;
không ký tự ngoài trang. Đã render và xem bố cục toàn bộ {pages} trang, xem riêng
các trang quan trọng ở cỡ lớn; sửa chồng chữ Hình3.3, mục lục/danh sách bảng tràn
một dòng và nới bảng đánh đổi. Chi tiết [visual review](C:/RideBoundContinuation/2026-10-15/thesis-v5/chung-cu/VISUAL-REVIEW.md).
47/47 kiểm bảng bổ sung và các chênh lệch chính đạt;13 mục manifest kết quả gốc
khớp. Đây là kiểm từ extraction, không tự nhận giải mã raw/forecast độc lập
hoặc tính lại mọi số lịch sử trong toàn luận văn.

Không sửa mã C#, chạy lại mô phỏng/analysis, huấn luyện mô hình, đổi seal/env,
commit/push hoặc bật timer. Baseline .NET 1.033/1.034 với1knownfailure giữ nguyên.
File báo cáo thực tập đã kiểm băm trước/sau không đổi; hai repo E giữ chỉ đọc.
Các tệp khác trong thư mục luận văn không nằm trong payload được giữ nguyên.
Next: người dùng đọc bản cuối; công việc nghiên cứu/thực thi mới theo yêu cầu riêng.
'''


def replace_once(text, old, new):
    assert text.count(old) == 1, old
    return text.replace(old, new)


p18 = DOCS / '18-status-and-decision-log.md'
s = p18.read_text(encoding='utf-8')
old = next(line for line in s.splitlines() if line.startswith('> Tiếp phiên ngoài cây chính:'))
s = replace_once(s, old, f'> Tiếp phiên ngoài cây chính: {day} — ADR-078; nghiên cứu bổ sung hoàn tất, luận văn thật đã cập nhật {pages} trang theo yêu cầu người dùng. Có sao lưu, kiểm bảng47/47 và kiểm bố cục toàn PDF; hai lịch PAUSED, không nâng work package.')
s = replace_once(s, 'Resource boundary after20 runs preserved; thesis unchanged.', f'Resource boundary after20 runs preserved; actual thesis updated {day}, {pages} pages, backed up and verified.')
s = replace_once(s, '**Consequences:** Không đổi implementation/protocol, baseline/panel, H6/E1/', details+'\n**Consequences:** Không đổi implementation/protocol, baseline/panel, H6/E1/')

p19 = DOCS / '19-requirement-traceability.md'
t = p19.read_text(encoding='utf-8')
old = next(line for line in t.splitlines() if line.startswith('| Luận văn dễ đọc,'))
t = replace_once(t, old, f'| Luận văn dễ đọc, đủ đánh đổi và provenance | Bản thật {pages} trang đã cập nhật {day}; backup/receipt thesis-v5-deployment.json;47/47 kiểm bảng;visual review toàn PDF | Tên cấu hình tự nhiên, không stage jargon;giữ giới hạn/đối chứng bất lợi;QA không phải tái lập mọi số lịch sử |')
t = replace_once(t, 'luận văn thật chưa sửa và vẫn chờ owner duyệt bản PDF', f'luận văn thật đã cập nhật theo yêu cầu mới, có sao lưu và receipt ngày{day}')

profile = DOCS / 'benchmarking/thesis-stage1b-continuation-2026-10-15.md'
u = profile.read_text(encoding='utf-8')
u = u.replace('\n\nADR-078', f'\n\n> Trạng thái mới nhất {day}: nghiên cứu đã xong; bản luận văn thật {pages} trang đã cập nhật theo yêu cầu. Các mốc chờ duyệt bên dưới là lịch sử.\n\nADR-078',1)
u += '\n\n'+details

resume = ROOT / 'RESUME-STATUS.md'
v = resume.read_text(encoding='utf-8')
start = v.index('> **TRẠNG THÁI MỚI NHẤT')
end = v.index('\n\n## Yêu cầu giữ nguyên', start)
v = v[:start]+f'''> **TRẠNG THÁI MỚI NHẤT {when}: LUẬN VĂN THẬT ĐÃ CẬP NHẬT.**
> PDF {pages} trang tại E:/Code/Report_INT3508/ridebound/main.pdf, có backup/receipt trên C.
> Nội dung gọi ưu tiên ổn định/ưu tiên chi phí, đủ đánh đổi và lý do phép so bổ sung.
> Kiểm bảng47/47; render và xem bố cục toàn bộ PDF; hash/chi tiết ở closure cuối.
> Nghiên cứu68/68/extract/report vẫn hoàn tất; H7=7<10, không Stage2; hai lịch PAUSED.
> Không rerun/overwrite study outputs, sửa frozen files hoặc commit/push. Các mốc cũ bên dưới là lịch sử.''' + v[end:]
v = replace_once(v, 'Bản luận văn thật còn chờ người dùng duyệt bản ghép.', f'Người dùng đã yêu cầu cập nhật nốt toàn bộ; bản luận văn thật đã được sao lưu và cập nhật ngày{day}.')
v += '\n\n'+details

for path, content in [(p18,s),(p19,t),(profile,u),(resume,v)]:
    path.write_text(content,encoding='utf-8')
print(json.dumps(dict(updated=[str(p) for p in [p18,p19,profile,resume]], pages=pages, pdfSha256=digest),ensure_ascii=False,indent=2))

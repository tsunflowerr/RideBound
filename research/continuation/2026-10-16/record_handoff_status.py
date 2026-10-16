from pathlib import Path
import datetime,json,shutil,subprocess

root=Path('C:/RideBoundContinuation/2026-10-16')
cap=root/'cost-cap-sensitivity-v1';out=Path('C:/RideBoundData/research/tier3-cost-cap-sensitivity-v1')
ride=Path('C:/Code/RideBound-handoff-20261016');report=Path('C:/Code/Report_INT3508-handoff-20261016')
stamp=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=7))).isoformat()
counts={}
for phase in ('smoke','main'):
    passed=0
    for s in (out/phase).glob('*/summary.json'):
        r=s.parent/'run-00.json'
        if r.exists() and json.loads(s.read_text()).get('status')=='pass' and json.loads(r.read_text()).get('status')=='succeeded':passed+=1
    starts=list((cap/'logs'/phase).glob('*.started.json'));finishes=list((cap/'logs'/phase).glob('*.finished.json'))
    counts[phase]={'pass':passed,'started':len(starts),'finished':len(finishes),'activeOrUnfinished':len(starts)-len(finishes)}
commits={p.name:subprocess.check_output(['git','-C',str(p),'rev-parse','HEAD']).decode().strip() for p in (ride,report)}
state={'observedAt':stamp,'counts':counts,'manifestSha256':'cedecbb1a97d31ec8ab295fd16c99fb607f14d622779247d8df6d9b219b42d6f',
       'launcherPid':14260,'driverPid':11224,'driverStartedAt':'2026-10-16T14:01:08+07:00',
       'publishedCommitsBeforeCheckpoint':commits,'floorsGiB':{'C':30,'E':40},
       'freeGiB':{drive:round(shutil.disk_usage(drive+':/').free/1024**3,3) for drive in ('C','E')},
       'localFailureReceipts':[p.name for p in cap.glob('*-FAILED.txt')]+[p.name for p in (cap/'STOP',cap/'INPUT-DRIFT-STOP.txt') if p.exists()],
       'mainTotal':68,'smokeTotal':2,'hourlyAutomation':'ACTIVE','oneoffAutomation':'PAUSED'}
(cap/'checkpoint.json').write_text(json.dumps(state,indent=1)+'\n',encoding='utf-8')
text=f'''# C60/C120 — trạng thái tiếp tục\n\nCheckpoint **{stamp}**. Driver11224/launcher14260 khởi động14:01:08+07.\nFull freeze exit0 lúc14:00:29; full preflight trước chạy PASS.\nSmoke: {counts['smoke']['pass']}/2pass, {counts['smoke']['activeOrUnfinished']}active/chưa kết thúc.\nMain: {counts['main']['pass']}/68pass, {counts['main']['started']}đã nhận; chưa có báo cáo kết quả mới.\n\nManifest: `{state['manifestSha256']}`. Selftest13/13; .NET1033pass/1034,\n1known clean-tree failure,exit1. Root log: run-20261016-1401.log.\nC còn{state['freeGiB']['C']}GiB,E còn{state['freeGiB']['E']}GiB tại checkpoint.\nKhông có receipt lỗi/dừng mới tại checkpoint. Trước lệnh tiếp theo phải kiểm\nprocess/log/output hiện tại; checkpoint không phải trạng thái thời gian thực.\n\n## GitHub đã push\n\n- [Mã, kết quả và audit](https://github.com/tsunflowerr/RideBound/tree/codex/research-handoff-20261016),\n  commit đã push trước checkpoint: `{commits[ride.name]}`.\n- [Khóa luận và chương trình/báo cáo nghiên cứu](https://github.com/tsunflowerr/Report_INT3508/tree/codex/ridebound-handoff-20261016),\n  commit `{commits[report.name]}`. PDF77trang giữ hash26c8dea1…1dd69.\n- Nguồn runner riêng `research/tier3-cost-first` đã push dce84ec.\n- Đọc START-HERE.md ở hai repo. GitHub giữ nguồn/báo cáo/hash; raw lớn,\n  dữ liệu/runtime/vendor và toàn văn paper ở máy gốc. Không có xóa dữ liệu.\n\n## Tiếp tục an toàn\n\nDriver tự làm2smoke→68main→analysis. Đủ68 và final checks mới có báo cáo.\nKhông tạo driver trùng; không retry failed/partial/anchor-only hoặc analysis\nstarted dở dang. STOP/*-FAILED.txt/INPUT-DRIFT-STOP.txt của đợt mới/cũ giữ nguyên.\nChỉ resume pending sau resource deferral khi driver và children đã hết, không\npartial/anchor-only, AC và C30/E40GiB đủ,410MiB/job, tối đa6workers.\n\nHidden Windows PowerShell -NoProfile -ExecutionPolicy Bypass -File\nC:/RideBoundContinuation/2026-10-16/cost-cap-sensitivity-v1/launch.ps1\n-Mode run -Log <log C mới,exclusive> -ManifestSha256 <hash trên>.\nKhông dùng launcher Stage1b/resource30 cũ. Không sửa code/plan/manifest đã khóa.\n\nHourly automation theo-d-i-ridebound-stage-1b-theo-gi ACTIVE cho đợt mới;\none-off06:20 PAUSED. Sau báo cáo đầy đủ và push kết quả thì tắt hourly, không\narchive. Owner đã cho commit/push báo cáo nhẹ liên quan, không force-push hoặc\nmerge main. Không sửa luận văn từ partial results, sửa dự báo, mở per-step/Stage2\nhoặc cứu H6/E1/H7. Audit mức sẵn sàng bảo vệ vẫn còn mở.\n'''
(cap/'CURRENT-STATUS.md').write_text(text,encoding='utf-8')
block=f'''\n\n## GitHub đã bàn giao, C60/C120 đang chạy — {stamp}\n\nĐã push research/tier3-cost-first=dce84ec; hai nhánh bàn giao codex đã push\nthành công: RideBound {commits[ride.name]}, Report_INT3508 {commits[report.name]}.\nĐọc START-HERE.md; raw/env/vendor/paper giữ local, nguồn/báo cáo/hash lên GitHub.\nFreeze manifest cedecbb1…42d6f đạt; driver11224/launcher14260 từ14:01:08.\nSmoke{counts['smoke']['pass']}/2pass,{counts['smoke']['activeOrUnfinished']}active/chưa kết thúc;\nmain{counts['main']['pass']}/68pass. Không có lỗi/dừng mới tại checkpoint.\n13/13 selftests; .NET1033/1034 với1knownfailure,exit1.\nHourly ACTIVE cho đợt mới;one-off PAUSED. C30/E40GiB/AC/410MiB giữ nguyên.\nCheckpoint local chi tiết: cost-cap-sensitivity-v1/CURRENT-STATUS.md vàcheckpoint.json.\nKhông clone rồi chạy trùng trên máy công ty. Không phân tích partial hoặc đổi\nfrozen inputs;driver tự đi tiếp sau2smoke. Chưa nhận có kết quả C60/C120.\n'''
with (Path('C:/RideBoundContinuation/2026-10-15/RESUME-STATUS.md')).open('a',encoding='utf-8') as f:f.write(block)
status=Path('C:/Code/RideBound-noworse/docs/18-status-and-decision-log.md')
s=status.read_text(encoding='utf-8')
s=s.replace('đang chuẩn bị/kiểm đầu vào nghiên cứu mới, chưa có kết quả mới','đã push hai nhánh bàn giao; đang chạy smoke C60/C120, chưa có kết quả mới',1)
s=s.replace('Results reported; both timers PAUSED.','Results reported; hourly was paused at closure and is now ACTIVE for ADR079; one-off PAUSED.',1)
if '| Cost-cap sensitivity / GitHub handoff |' not in s:
    lines=s.splitlines()
    index=next(i for i,l in enumerate(lines) if l.startswith('| Thesis / Stage 1b continuation |'))
    lines.insert(index+1,'| Cost-cap sensitivity / GitHub handoff | ADR079: both codex branches pushed; source dce84ec pushed; new manifest cedecbb1…42d6f,13/13 selftests,full preflight PASS;2 smoke active before68 main. Hourly ACTIVE; no new outcome yet. [Hồ sơ](benchmarking/cost-cap-sensitivity-handoff-2026-10-16.md) |')
    s='\n'.join(lines)+'\n'
status.write_text(s+block,encoding='utf-8')
trace=Path('C:/Code/RideBound-noworse/docs/19-requirement-traceability.md')
s=trace.read_text(encoding='utf-8').replace('Đang chuẩn bị push; không rewrite lịch sử','Đã push hai nhánh codex; không rewrite lịch sử',1)
s=s.replace('Full input check đang chạy; chưa có kết quả;2 smoke trước68main','Full freeze/preflight PASS;2 smoke active;0/68main;chưa có kết quả mới',1)
trace.write_text(s+block,encoding='utf-8')
record=Path('C:/Code/RideBound-noworse/docs/benchmarking/cost-cap-sensitivity-handoff-2026-10-16.md')
with record.open('a',encoding='utf-8') as f:f.write(block)
for rel in ['docs/18-status-and-decision-log.md','docs/19-requirement-traceability.md','docs/benchmarking/cost-cap-sensitivity-handoff-2026-10-16.md']:
    shutil.copy2(Path('C:/Code/RideBound-noworse')/rel,ride/rel)
dest=ride/'research/continuation/2026-10-16/cost-cap-sensitivity-v1'
for path in cap.iterdir():
    if path.is_file() and path.name!='instance.lock':shutil.copy2(path,dest/path.name)
shutil.copy2(Path('C:/RideBoundContinuation/2026-10-15/RESUME-STATUS.md'),ride/'research/continuation/2026-10-15/RESUME-STATUS.md')
for name in ('extended-secret-scan.json','record_handoff_status.py'):
    shutil.copy2(root/name,ride/'research/continuation/2026-10-16'/name)
print(json.dumps(state))

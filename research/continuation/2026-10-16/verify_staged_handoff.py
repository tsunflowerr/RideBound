from pathlib import Path
import hashlib,json,re,subprocess

roots=[Path('C:/Code/RideBound-handoff-20261016'),Path('C:/Code/Report_INT3508-handoff-20261016')]
patterns=[re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
          re.compile(rb'\b(?:gh[pousr]_[A-Za-z0-9]{36,255}|github_pat_[A-Za-z0-9_]{60,255})\b'),
          re.compile(rb'\bAKIA[A-Z0-9]{16}\b'),re.compile(rb'\bxox[baprs]-[A-Za-z0-9-]{20,}\b')]
results=[]
for root in roots:
    names=[p for p in subprocess.check_output(['git','-C',str(root),'diff','--cached','--name-only','-z']).decode('utf-8').split('\0') if p]
    proc=subprocess.Popen(['git','-C',str(root),'cat-file','--batch'],stdin=subprocess.PIPE,stdout=subprocess.PIPE)
    problems=[];preserved=0;largest=0;size=0
    for name in names:
        proc.stdin.write((':'+name+'\n').encode('utf-8'));proc.stdin.flush()
        head=proc.stdout.readline().decode().rstrip().split()
        if len(head)!=3 or head[1]!='blob':raise RuntimeError(repr(head))
        length=int(head[2]);data=proc.stdout.read(length)
        if len(data)!=length or proc.stdout.read(1)!=b'\n':raise RuntimeError('cat-file framing')
        largest=max(largest,length);size+=length
        if length>=100*1024*1024:problems.append([name,'large staged blob'])
        if any(p.search(data) for p in patterns):problems.append([name,'credential/private-key pattern'])
        protected=name.startswith(('research/continuation/','ridebound/','ridebound-scratchpad/'))
        if protected:
            preserved+=1
            if hashlib.sha256(data).digest()!=hashlib.sha256((root/name).read_bytes()).digest():problems.append([name,'newline/byte transformation'])
    proc.stdin.close();proc.wait()
    item={'repository':str(root),'stagedFiles':len(names),'stagedMiB':round(size/1048576,2),'largestMiB':round(largest/1048576,2),
          'exactEvidenceBytesChecked':preserved,'problems':problems}
    print(json.dumps(item));results.append(item)
out=Path('C:/RideBoundContinuation/2026-10-16/staged-handoff-verification.json');out.write_text(json.dumps(results,indent=1)+'\n',encoding='utf-8')
if any(r['problems'] for r in results):raise SystemExit(1)

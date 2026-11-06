"""Import immutable *calibration* evidence. Never turns a hand-copied model into a Git baseline."""
import hashlib, json, shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'BlockGame.Tests' / 'TestData' / 'BenchmarkControls'
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def fingerprint(value):
    return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest().upper()
def import_run(name, directory, predicate=lambda r: True, pair=False):
    target = DEST/name
    target.mkdir(parents=True,exist_ok=True)
    manifest = load(directory/'manifest.json')
    shutil.copyfile(directory/'manifest.json',target/'original-manifest.json')
    observations=[]
    host=None
    for path in sorted((directory/'results').glob('*full.json')):
        data=load(path)
        selected=[r for r in data['Benchmarks'] if predicate(r)]
        if not selected: continue
        shutil.copyfile(path,target/path.name)
        host=data['HostEnvironmentInfo']
        for row in selected:
            scenario=row['Parameters'].removeprefix('Scenario=')
            family=row['Type']
            if pair:
                family='selection-movement'
            inputs={'legacyScenario':scenario,'legacyFamily':family,'workloadSource':manifest['source']['workloadSourceSha256']}
            timing='early-return' if scenario=='same' else 'diagnostic' if scenario.startswith('outside-') else 'normal'
            contract={'id':family+'/'+scenario,'version':1,'unit':'closed-path' if pair else 'single-operation',
                      'inputs':inputs,'inputFingerprint':fingerprint(inputs),'timingClass':timing}
            observations.append({'caseId':row['FullName'],'scenario':contract,'implementation':row['Type'],
                'samplesNs':row['Statistics']['OriginalValues'], 'bytesPerOperation':row.get('Memory',{}).get('BytesAllocatedPerOperation'),
                'diagnostics':[], 'success':True})
    env={k:host[k] for k in ('ProcessorName','RuntimeVersion','Architecture','DotNetCliVersion','OsVersion')}
    env.update(machine=manifest['host']['machine'],power=manifest['powerPlan'],native=manifest['dependencies']['nativeSha256'])
    run={'schemaVersion':2,'kind':'calibration','mode':'Measure','engineRevision':manifest['source']['commit'],
         'engineClean':False,'harnessFingerprint':manifest['source']['harnessSourceSha256'],
         'environmentFingerprint':fingerprint(env),'correctnessPassed':True,'sourceUnchanged':manifest['sourceUnchangedDuringRun'],
         'cases':observations,'provenance':'Imported historical trial evidence; models/dirty source; comparator tests ONLY. Original manifest SHA256 '+sha(directory/'manifest.json'),
         'evidencePaths':[p.name for p in target.iterdir() if p.is_file()]}
    (target/'benchmark-run.json').write_text(json.dumps(run,indent=2)+'\n',encoding='utf-8')

import_run('null-a',ROOT/'out/benchmark-implementation/baseline-a/benchmarks')
import_run('null-b',ROOT/'out/benchmark-implementation/baseline-b-retry/benchmarks')
common=ROOT/'out/selection-trial/common-harness/benchmarks'
import_run('positive-before',common,lambda r:r['Type']=='SelectionRebuildBenchmarks' and r['Parameters']=='Scenario=walk-x',True)
import_run('positive-after',common,lambda r:r['Type']=='SelectionDeltaBenchmarks' and r['Parameters']=='Scenario=walk-x',True)
import_run('early-before',common,lambda r:r['Type']=='SelectionRebuildBenchmarks' and r['Parameters']=='Scenario=same',True)
import_run('early-after',common,lambda r:r['Type']=='SelectionDeltaBenchmarks' and r['Parameters']=='Scenario=same',True)
print('Imported null, noisy, positive and early-return calibration evidence')

negative=ROOT/'out/pipeline-negative-control/benchmarks'
if (negative/'benchmark-run.json').exists():
    measured=load(negative/'benchmark-run.json')
    for name,slow in [('negative-before',False),('negative-after',True)]:
        target=DEST/name;target.mkdir(parents=True,exist_ok=True)
        run=dict(measured)
        run['cases']=[r for r in measured['cases'] if ('SlowControl' in r['implementation'])==slow]
        assert len(run['cases'])==1
        run['kind']='calibration'
        run['provenance']='Measured extra-pass regression control; comparator validation ONLY; original normalized run SHA256 '+sha(negative/'benchmark-run.json')
        shutil.copyfile(negative/'benchmark-run.json',target/'original-run.json')
        for path in (negative/'results').glob('*full.json'): shutil.copyfile(path,target/path.name)
        run['evidencePaths']=[p.name for p in target.iterdir() if p.is_file()]
        (target/'benchmark-run.json').write_text(json.dumps(run,indent=2)+'\n',encoding='utf-8')
    print('Imported measured deliberate slowdown control')

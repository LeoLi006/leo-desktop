"""Summarize only allowlisted numeric counters; never read app/ or user data."""
import json
import pathlib
import statistics

ROOT = pathlib.Path(__file__).resolve().parents[3]
DATA = ROOT / '.build' / 'q1'


def bounds(values):
    values = list(values)
    return {'min': min(values), 'median': statistics.median(values), 'max': max(values)}


def run():
    summary = {'format': 'leo-q1-summary-1', 'profiles': []}
    lines = ['# Q1 测量摘要', '', '自动汇总，仅包含允许的数值计数。各模式限制见 BASELINE.md；不将短样本解释为长期稳定结论。', '', '| 模式 / 场景 | 单核 CPU % 中位数 [范围] | 私有内存 MiB 中位数 | GDI / USER 中位数 | 帧数中位数 |', '| --- | --- | --- | --- | --- |']
    for profile in ('preview', 'companion', 'common'):
        files = sorted(DATA.glob(profile+'-*.json'))
        if not files:
            continue
        rows = [json.loads(f.read_text(encoding='utf-8-sig')) for f in files]
        if not all(r['ok'] for r in rows):
            raise ValueError('Failed baseline report')
        entry = {'profile': profile, 'runs': len(rows), 'runtime': rows[0]['runtime'], 'logical_processors': rows[0]['logical_processors'], 'construction_to_show_ms': bounds(r['pet_construction_to_first_show_ms'] for r in rows), 'quick_note_show_ms': bounds(v for r in rows for v in r['quick_note_show_ms']), 'module_disable_cancel_ms': bounds(v for r in rows for v in r['module_disable_cancel_ms']), 'scenes': []}
        for index, scene in enumerate(('idle', 'docked', 'hidden', 'windows')):
            samples = [r['samples'][index] for r in rows]
            if any(s['scene'] != scene for s in samples):
                raise ValueError('Scene order mismatch')
            item = {'scene': scene, **{key: bounds(s[key] for s in samples) for key in ('seconds', 'cpu_one_core_percent', 'cpu_machine_percent', 'allocated_bytes', 'private_bytes_after', 'handles_after', 'gdi_after', 'user_after', 'frames', 'pump_gap_p95_ms')}}
            entry['scenes'].append(item)
            cpu = item['cpu_one_core_percent']
            lines.append(f"| {profile} / {scene} | {cpu['median']:.2f} [{cpu['min']:.2f}–{cpu['max']:.2f}] | {item['private_bytes_after']['median']/1048576:.1f} | {item['gdi_after']['median']:g} / {item['user_after']['median']:g} | {item['frames']['median']:g} |")
        summary['profiles'].append(entry)
    live_file = DATA / 'live.json'
    if live_file.exists():
        rows = json.loads(live_file.read_text(encoding='utf-8-sig'))['samples']
        summary['passive_live'] = {'seconds': rows[-1]['seconds'], 'cpu_ms': rows[-1]['cpu_ms'], 'cpu_one_core_percent': rows[-1]['cpu_ms']/(rows[-1]['seconds']*10), **{key: bounds(r[key] for r in rows) for key in ('private_bytes', 'working_set', 'handles', 'threads')}}
    (DATA / 'measurements.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
    (DATA / 'MEASURED.md').write_text('\n'.join(lines)+'\n', encoding='utf-8')
    print(json.dumps({'profiles': len(summary['profiles']), 'runs': sum(p['runs'] for p in summary['profiles']), 'passive_live': 'passive_live' in summary}))


if __name__ == '__main__':
    run()

"""Compare the unchanged isolated-preview method against the Q1 numeric snapshot."""
import json
import pathlib
import statistics

ROOT = pathlib.Path(__file__).resolve().parents[3]
DATA = ROOT / '.build' / 'q2'
BASE = ROOT / 'src' / 'Architecture' / 'Q1' / 'measurements.json'


def run():
    baseline = next(p for p in json.loads(BASE.read_text(encoding='utf-8'))['profiles'] if p['profile'] == 'preview')
    rows = [json.loads(f.read_text(encoding='utf-8-sig')) for f in sorted(DATA.glob('preview-*.json'))]
    if len(rows) < 3 or not all(r['ok'] and r['runtime'] == baseline['runtime'] and r['mode'] == 'isolated-preview' for r in rows):
        raise ValueError('Need three successful samples on the same runtime/method')
    comparisons = []
    def compare(label, old, values):
        current = statistics.median(values)
        threshold = max(old['median'] * .2, old['max'] - old['min'])
        comparisons.append({'metric': label, 'baseline_median': old['median'], 'current_median': current, 'baseline_range': [old['min'], old['max']], 'investigation_threshold': threshold, 'investigate': current - old['median'] > threshold})
    compare('construction_to_show_ms', baseline['construction_to_show_ms'], [r['pet_construction_to_first_show_ms'] for r in rows])
    compare('quick_note_show_ms', baseline['quick_note_show_ms'], [v for r in rows for v in r['quick_note_show_ms']])
    compare('module_disable_cancel_ms', baseline['module_disable_cancel_ms'], [v for r in rows for v in r['module_disable_cancel_ms']])
    handles = []
    for i, scene in enumerate(baseline['scenes']):
        samples = [r['samples'][i] for r in rows]
        if any(s['scene'] != scene['scene'] for s in samples):
            raise ValueError('Scene mismatch')
        for key in ('cpu_one_core_percent', 'allocated_bytes', 'private_bytes_after', 'pump_gap_p95_ms'):
            compare(scene['scene'] + '/' + key, scene[key], [s[key] for s in samples])
        handles.append({'scene': scene['scene'], **{key: {'baseline': scene[key]['median'], 'current': statistics.median(s[key] for s in samples)} for key in ('handles_after', 'gdi_after', 'user_after', 'frames')}})
    result = {'scope': 'isolated preview only; not real application launch speed or real desktop background load', 'runs': len(rows), 'comparisons': comparisons, 'resources': handles, 'needs_investigation': any(c['investigate'] for c in comparisons)}
    controls = {}
    for kind in ('old', 'new'):
        files = sorted(DATA.glob('matched-'+kind+'-*.json'))
        if len(files) == 3:
            samples = [json.loads(f.read_text(encoding='utf-8-sig')) for f in files]
            if not all(r['ok'] and r['runtime'] == baseline['runtime'] for r in samples):
                raise ValueError('Matched runtime/method mismatch')
            values = [r['pet_first_show_ms'] for r in samples]
            controls[kind] = {'values_ms': values, 'median_ms': statistics.median(values)}
    if set(controls) == {'old', 'new'}:
        method = json.loads((DATA / 'control-method.json').read_text(encoding='utf-8-sig'))
        if not method['same_assets'] or method['old_binary_hash'] == method['new_binary_hash']:
            raise ValueError('Control must have same assets and different binaries')
        old = controls['old'];limit = max(old['median_ms']*.2, max(old['values_ms'])-min(old['values_ms']))
        controls.update({'threshold_ms': limit, 'new_startup_regression': controls['new']['median_ms']-old['median_ms'] > limit, 'method': method['method']})
        result['matched_startup_control'] = controls
        result['unresolved_regression'] = controls['new_startup_regression'] or any(c['investigate'] and c['metric'] != 'construction_to_show_ms' for c in comparisons)
    (DATA / 'comparison.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    lines = ['# Q2 与 Q1 的定向性能比较', '', '相同隔离预览方法，各3个新进程；不代表真实软件启动速度，也不代表个人配置负载。调查门槛沿用 Q1：max(基线中位数20%，基线样本极差)。', '', '| 指标 | Q1 中位数 | Q2 中位数 | 超过预定调查门槛 |', '| --- | --- | --- | --- |']
    for row in comparisons:
        lines.append(f"| {row['metric']} | {row['baseline_median']:.3f} | {row['current_median']:.3f} | {'是' if row['investigate'] else '否'} |")
    lines += ['', '句柄、GDI/USER 及绘制帧数见 comparison.json；短时采样不证明长期无泄漏。CPU 为单核百分比，内存/分配单位为字节，其他耗时单位为毫秒。']
    if 'matched_startup_control' in result:
        lines += ['', '## 首次显示的同场新旧控制', '', f"历史首次显示项仍保留越限标记。使用未改动的 --performance-test 对安装中的旧 DLL 与新 DLL 交替采样各3次，显式使用同一运行时/依赖，校验关键图集一致。旧版中位数 {controls['old']['median_ms']:.3f} ms，新版 {controls['new']['median_ms']:.3f} ms，预定同场调查门槛 {controls['threshold_ms']:.3f} ms。", '', '同场新版本回归：'+('是' if controls['new_startup_regression'] else '未复现')+'。历史绝对耗时变化仍需后续跟踪；不据此宣称已找到系统环境变化的具体原因，也不将这一组较短方法与 Q1 原始数值直接合并。']
    (DATA / 'COMPARISON.md').write_text('\n'.join(lines)+'\n', encoding='utf-8')
    print(json.dumps({'runs': len(rows), 'needs_investigation': result['needs_investigation'], 'unresolved_regression': result.get('unresolved_regression'), 'matched_startup_control': controls, 'flagged': [c['metric'] for c in comparisons if c['investigate']]}))


if __name__ == '__main__':
    run()

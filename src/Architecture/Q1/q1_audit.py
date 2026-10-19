"""Offline Q1 draft validator and source inventory; Python standard library only.

Does not import Leo, inspect app/, install modules, or execute any sample action.
Only the deliberately small schema subset documented in CONTRACT.md is supported.
"""
import copy
import datetime as dt
import json
import pathlib
import re

HERE = pathlib.Path(__file__).resolve().parent
SRC = HERE.parents[1]
OUT = SRC.parent / '.build' / 'q1'


def require(value, message):
    if not value:
        raise ValueError(message)


def keys(value, fields):
    require(isinstance(value, dict) and set(value) == set(fields.split()), 'unknown/missing fields')


def typed(schema, value):
    require(set(schema) <= {'type', 'properties', 'required', 'additionalProperties', 'enum', 'items', 'format'}, 'unknown schema keyword')
    kind = schema['type']
    classes = {'object': dict, 'array': list, 'string': str, 'integer': int, 'boolean': bool}
    require(kind in classes and type(value) is classes[kind], 'type mismatch')
    if 'enum' in schema:
        require(value in schema['enum'], 'enum mismatch')
    if kind == 'object':
        require(schema['additionalProperties'] is False, 'explicit closed object required')
        props = schema['properties']
        require(set(schema['required']) <= set(props), 'required property undeclared')
        require(set(schema['required']) <= set(value) <= set(props), 'unknown/missing parameter')
        for name, item in value.items():
            typed(props[name], item)
    if kind == 'array':
        for item in value:
            typed(schema['items'], item)
    if 'format' in schema:
        require(kind == 'string', 'format requires string')
        if schema['format'] == 'date':
            require(re.fullmatch(r'\d{4}-\d{2}-\d{2}', value), 'date format')
            dt.date.fromisoformat(value)
        elif schema['format'] == 'date-time':
            require('T' in value, 'datetime separator')
            require(dt.datetime.fromisoformat(value.replace('Z', '+00:00')).utcoffset() is not None, 'timezone required')
        else:
            raise ValueError('unknown format')


def owner(action):
    if action.startswith('football_') or action == 'match_add':
        return 'football'
    for prefix, module in [('music_', 'music'), ('media_', 'media'), ('proxy_', 'clash')]:
        if action.startswith(prefix):
            return module
    if action == 'terminal_open':
        return 'terminal'
    return 'edge' if action in ('web_search', 'web_open', 'watch_open') else 'core'


def validate(example, names):
    keys(example, 'module request result')
    module, request, result = (example[k] for k in ('module', 'request', 'result'))
    keys(module, 'format id name moduleVersion hostRange protocol runtime dependencies enablement configVersion dataOwner actions contributions')
    require(module['format'] == 'leo-capability-draft/0' and module['protocol'] == 'leo-action-draft/0' and module['hostRange'] == 'draft-only', 'unsupported draft version')
    require(re.fullmatch(r'[a-z][a-z0-9-]{1,39}', module['id']), 'invalid module id')
    require(re.fullmatch(r'\d+\.\d+\.\d+', module['moduleVersion']), 'invalid module version')
    require(isinstance(module['name'], str) and module['name'].strip(), 'invalid name')
    require(module['runtime'] in ('builtin-adapter', 'worker') and module['enablement'] == 'host-controlled', 'invalid runtime/enablement')
    require(type(module['configVersion']) is int and module['configVersion'] >= 1 and module['dataOwner'] == module['id'], 'invalid data owner/version')
    for dep in module['dependencies']:
        keys(dep, 'id required')
        require(isinstance(dep['id'], str) and dep['id'] and type(dep['required']) is bool, 'invalid dependency')
    require(module['actions'] and len({a['id'] for a in module['actions']}) == len(module['actions']), 'duplicate/empty action')
    for action in module['actions']:
        keys(action, 'id description legacyOwner input output timeoutMs effect idempotency concurrency cancel retry compensation evidence')
        require(action['id'] in names and action['legacyOwner'] == owner(action['id']), 'legacy mapping mismatch')
        require(type(action['timeoutMs']) is int and 1 <= action['timeoutMs'] <= 120000, 'invalid timeout')
        require(action['effect'] in ('none', 'ui', 'local-write', 'external'), 'invalid effect')
        require(action['idempotency'] in ('idempotent', 'non-idempotent'), 'invalid idempotency')
        keys(action['concurrency'], 'resource policy')
        require(action['concurrency']['resource'] and action['concurrency']['policy'] in ('share-read', 'serial', 'independent'), 'invalid concurrency')
        require(action['cancel'] in ('cooperative', 'process-terminate', 'unsupported') and action['compensation'] == 'none', 'invalid cancellation/compensation')
        require(action['retry'] in ('none', 'transient-read-only'), 'invalid retry')
        require(action['effect'] == 'none' or action['retry'] == 'none', 'unsafe retry')
    for contribution in module['contributions']:
        keys(contribution, 'kind actionId title when')
        require(contribution['kind'] in ('search', 'menu', 'shortcut', 'settings') and contribution['when'] == 'enabled' and contribution['title'], 'invalid contribution')
        require(contribution['actionId'] in {a['id'] for a in module['actions']}, 'unknown contribution action')
    keys(request, 'protocol requestId traceId parentRequestId source actionId parameters deadline')
    require(request['protocol'] == module['protocol'] and request['source'] in ('search', 'chat', 'voice', 'combo', 'ui', 'background'), 'invalid request version/source')
    require(all(isinstance(request[k], str) and request[k] for k in ('requestId', 'traceId')), 'invalid correlation')
    require(request['parentRequestId'] is None or isinstance(request['parentRequestId'], str), 'invalid parent')
    typed({'type': 'string', 'format': 'date-time'}, request['deadline'])
    action = next(a for a in module['actions'] if a['id'] == request['actionId'])
    typed(action['input'], request['parameters'])
    keys(result, 'requestId status data error evidence retryable')
    require(result['requestId'] == request['requestId'] and type(result['retryable']) is bool, 'invalid result correlation')
    require(result['status'] in ('succeeded', 'failed', 'cancelled', 'skipped', 'partial', 'needs-input'), 'invalid result status')
    keys(result['evidence'], 'level kind')
    require(result['evidence']['level'] in ('confirmed', 'accepted', 'unknown') and result['evidence']['kind'], 'invalid evidence')
    if result['status'] == 'succeeded':
        require(result['error'] is None, 'success has error')
        typed(action['output'], result['data'])
    else:
        require(result['evidence']['level'] == 'unknown', 'failure/cancel has success evidence')
        if result['status'] == 'failed':
            keys(result['error'], 'code message')
            require(result['error']['code'] and result['error']['message'], 'failure missing error')
        if result['status'] == 'partial':
            typed({'type':'object','properties':{'completed':{'type':'array','items':{'type':'string'}},'remaining':{'type':'array','items':{'type':'string'}}},'required':['completed','remaining'],'additionalProperties':False}, result['data'])
        if result['status'] == 'needs-input':
            typed({'type':'object','properties':{'choices':{'type':'array','items':{'type':'object','properties':{'id':{'type':'string'},'title':{'type':'string'}},'required':['id','title'],'additionalProperties':False}}},'required':['choices'],'additionalProperties':False}, result['data'])
            require(result['data']['choices'], 'empty choices')


def run():
    OUT.mkdir(parents=True, exist_ok=True)
    text = (SRC / 'LeoActions.cs').read_text(encoding='utf-8-sig')
    names = re.findall(r'"([a-z_]+)"', re.search(r'Names=\[(.*?)\];', text, re.S).group(1))
    require(len(names) == len(set(names)), 'duplicate current action IDs')
    # Check the classifier snapshot against its source representation; this is
    # a drift sentinel, not an independent implementation test of ModuleCatalog.
    catalog = (SRC / 'ActionDispatcher.cs').read_text(encoding='utf-8-sig')
    require(all(s in catalog for s in ['action.StartsWith("football_")', 'action=="match_add"', 'action.StartsWith("music_")', 'action.StartsWith("media_")', 'action.StartsWith("proxy_")', 'action=="terminal_open"', '"web_search" or "web_open" or "watch_open"']), 'review classifier after source drift')
    sources = {f.name: f.read_text(encoding='utf-8-sig') for f in SRC.glob('*.cs') if 'Tests' not in f.name and not f.name.endswith('Probe.cs')}
    inventory = [{'action': name, 'current_owner': owner(name), 'references': sorted(f for f, content in sources.items() if '"'+name+'"' in content and f != 'LeoActions.cs')} for name in names]
    # Dynamic action composition (e.g. "media_" + command) has no exact literal.
    # Keep those rows visible rather than misreporting them as dead capabilities.
    examples = json.loads((HERE / 'examples.json').read_text(encoding='utf-8'))
    require(len({e['module']['id'] for e in examples}) == len(examples), 'duplicate module')
    checks = {}
    for sample in examples:
        validate(sample, names)
        checks['valid_' + sample['module']['id']] = True
    checks['module_without_ui_supported'] = any(not e['module']['contributions'] for e in examples)
    # Mutations must be rejected, not merely parse successfully.
    def reject(name, change, base=0):
        sample = copy.deepcopy(examples[base]);change(sample)
        try:
            validate(sample, names)
        except (ValueError, KeyError, StopIteration, TypeError):
            checks[name] = True
        else:
            checks[name] = False
    reject('reject_unknown_descriptor_field', lambda e: e['module'].update(secret='forbidden'))
    reject('reject_unknown_protocol', lambda e: e['module'].update(protocol='leo-action/999'))
    reject('reject_unknown_format', lambda e: e['module'].update(format='unsupported'))
    reject('reject_duplicate_action', lambda e: e['module']['actions'].append(e['module']['actions'][0]))
    reject('reject_unknown_input', lambda e: e['request']['parameters'].update(path='not-accepted'))
    reject('reject_wrong_input_type', lambda e: e['request']['parameters'].update(app_id=12))
    reject('reject_missing_input', lambda e: e['request'].update(parameters={}))
    reject('reject_output_type', lambda e: e['result']['data'].update(started='true'))
    reject('reject_cancel_success_evidence', lambda e: e['result'].update(status='cancelled'))
    reject('reject_unknown_status', lambda e: e['result'].update(status='maybe'))
    reject('reject_side_effect_retry', lambda e: e['module']['actions'][0].update(retry='transient-read-only'))
    reject('reject_wrong_legacy_owner', lambda e: e['module']['actions'][0].update(legacyOwner='media'))
    reject('reject_invalid_day', lambda e: e['request']['parameters'].update(day='2030-02-30'), 1)
    reject('reject_timezone_missing', lambda e: e['request'].update(deadline='2030-01-01T12:00:00'))
    reject('reject_unknown_schema_type', lambda e: e['module']['actions'][0]['input'].update(type='any'))
    reject('reject_unknown_schema_keyword', lambda e: e['module']['actions'][0]['input'].update(unsupported=True))
    reject('reject_unknown_contribution_action', lambda e: e['module']['contributions'][0].update(actionId='missing'))
    reject('reject_result_request_mismatch', lambda e: e['result'].update(requestId='wrong'))
    for status in ('failed', 'cancelled', 'skipped', 'partial', 'needs-input'):
        sample = copy.deepcopy(examples[0]);sample['result'].update(status=status, data=None, evidence={'level':'unknown','kind':'not-confirmed'}, error={'code':'fixture_failure','message':'Fixture failure'} if status == 'failed' else None)
        if status == 'partial':
            sample['result']['data'] = {'completed':['fixture-step-1'],'remaining':['fixture-step-2']}
        if status == 'needs-input':
            sample['result']['data'] = {'choices':[{'id':'fixture-app','title':'示例应用'}]}
        validate(sample, names);checks['result_status_' + status] = True
    reject('reject_missing_partial_steps', lambda e: e['result'].update(status='partial', data=None, evidence={'level':'unknown','kind':'not-confirmed'}))
    reject('reject_empty_selection', lambda e: e['result'].update(status='needs-input', data={'choices':[]}, evidence={'level':'unknown','kind':'not-confirmed'}))
    report = {'ok': all(checks.values()), 'scope': 'draft structure and source inventory only; not new runtime compliance', 'checks': checks, 'action_count': len(names), 'actions': inventory}
    (OUT / 'audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    lines=['# Q1 当前动作清册', '', '由 `q1_audit.py` 从当前源码生成；只统计精确字符串引用，不是完整调用图。动态拼接仍须结合 INVENTORY.md 人工追踪。', '', '| 动作 ID | 当前归属 | 精确引用文件 |', '| --- | --- | --- |']
    for row in inventory:
        refs=', '.join(f'[{f}](../../{f})' for f in row['references']) or '动态组合/无精确引用，需人工追踪'
        lines.append(f"| `{row['action']}` | {row['current_owner']} | {refs} |")
    (OUT / 'ACTIONS.md').write_text('\n'.join(lines)+'\n', encoding='utf-8')
    print(json.dumps({'ok': report['ok'], 'checks': len(checks), 'actions': len(names)}))
    require(report['ok'], 'draft validation failed')


if __name__ == '__main__':
    run()

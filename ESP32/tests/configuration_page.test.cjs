// Run with: node tests/configuration_page.test.cjs
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../include/ConfigurationPage.h'), 'utf8')
  .match(/<script>([\s\S]*?)<\/script>/)[1];
const flush = () => new Promise(resolve => setImmediate(resolve));

async function page(overrides = {}, saveResponse = {ok: true, data: {success: true, message: 'Saved; restarting'}}) {
  const nodes = {}, requests = [];
  const element = id => nodes[id] ??= {
    value: '', disabled: false, checked: false, dataset: {}, textContent: '',
    // Treat accidental HTML insertion as a test failure.
    set innerHTML(_) { throw new Error('Unsafe HTML insertion'); },
    addEventListener(event, handler) { this[event] = handler; }
  };
  vm.runInNewContext(source, {
    document: {getElementById: element},
    fetch: async (url, options) => {
      requests.push({url, options});
      if (options.method) return {ok: saveResponse.ok, json: async () => saveResponse.data};
      return {ok: true, json: async () => ({
        internetSsid: '<img src=x onerror=alert(1)>', backendBaseUrl: 'http://example.test/api',
        gatewayId: 1, gatewayCode: 'GATEWAY-001', hasWifiPassword: true,
        hasApiKey: true, csrfToken: 'test-token', canChange: true, ...overrides
      })};
    }
  });
  await flush();
  return {element, requests, submit: () => element('settings').submit({preventDefault() {}})};
}

(async () => {
  const p = await page();
  assert.equal(p.element('internetSsid').value, '<img src=x onerror=alert(1)>');
  assert.equal(p.element('internetPassword').value, '');
  assert.equal(p.element('apiKey').value, '');
  assert.equal(p.element('fields').disabled, false);
  await p.submit();
  const request = p.requests[1];
  assert.equal(request.url, '/api/config');
  assert.equal(request.options.credentials, 'same-origin');
  assert.equal(request.options.headers['X-Config-Token'], 'test-token');
  assert.equal(request.options.headers['Content-Type'], 'application/json');
  const payload = JSON.parse(request.options.body);
  assert.equal(payload.internetPassword, '');
  assert.equal(payload.apiKey, '');
  assert.equal(payload.clearWifiPassword, false);
  assert.equal(payload.adoptPending, false);
  assert.equal(p.element('adoptPending').disabled, true);
  assert.equal(payload.gatewayId, 1);
  assert.equal(p.element('fields').disabled, true);
  assert.equal(p.element('save').disabled, true);
  await p.submit();
  assert.equal(p.requests.length, 2, 'No second write after a successful save');

  const locked = await page({canChange: false});
  assert.equal(locked.element('fields').disabled, true);
  assert.equal(locked.element('save').disabled, true);
  await locked.submit();
  assert.equal(locked.requests.length, 1, 'Locked state never posts');

  const adoption = await page({hasApiKey:false,canAdoptPending:true});
  assert.equal(adoption.element('adoptPending').disabled,false);
  assert.equal(adoption.element('adoptPending').checked,false);
  adoption.element('adoptPending').checked=true;
  await adoption.submit();
  assert.equal(JSON.parse(adoption.requests[1].options.body).adoptPending,true);

  const open = await page();
  open.element('internetPassword').value = 'test-password';
  open.element('clearWifiPassword').checked = true;
  open.element('clearWifiPassword').change();
  assert.equal(open.element('internetPassword').value, '');
  assert.equal(open.element('internetPassword').disabled, true);
  await open.submit();
  assert.equal(JSON.parse(open.requests[1].options.body).clearWifiPassword, true);

  const rejected = await page({}, {ok: false, data: {success: false, message: '<script>Rejected</script>'}});
  rejected.element('apiKey').value = 'test-provisioned-key';
  await rejected.submit();
  assert.equal(rejected.element('message').textContent, '<script>Rejected</script>');
  assert.equal(rejected.element('message').dataset.error, 'true');
  assert.equal(rejected.element('save').disabled, false, 'Allow correcting rejected settings');
  assert.equal(rejected.element('apiKey').value, 'test-provisioned-key');
  console.log('Configuration page: load, safe text, secrets, CSRF, locked state, open Wi-Fi, save and error flows passed');
})().catch(error => { console.error(error); process.exitCode = 1; });

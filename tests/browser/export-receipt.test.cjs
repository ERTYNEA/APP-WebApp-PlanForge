const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../src/PlanForge.Web/wwwroot/ui.js'), 'utf8');
for (const receipt of ['9007199254740993', '9223372036854775807', null]) {
 test(`download preserves exact opaque event receipt ${receipt}`, async () => {
  let clicked = false;
  const headers = new Headers({ 'Content-Type': 'text/csv', ...(receipt === null ? {} : { 'X-PlanForge-Change-Id': receipt }) });
  const context = { window: {}, fetch: async () => ({ ok: true, status: 200, redirected: false, headers, blob: async () => new Blob(['controlled fixture']) }), document: { createElement: () => ({ click: () => { clicked = true; } }) }, URL: { createObjectURL: () => 'blob:controlled', revokeObjectURL() {} }, setTimeout() {} };
  vm.runInNewContext(source, context);
  const result = await context.window.planForge.download('/files/csv/controlled');
  assert.equal(result.success, true);
  assert.equal(result.changeId, receipt);
  assert.equal(clicked, true);
 });
}

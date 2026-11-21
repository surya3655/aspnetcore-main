// Browser evidence viewer: endpoint responses are fetched from the real Kestrel host.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

(async () => {
  const [baseUrl, output, label] = process.argv.slice(2);
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 1100 }, deviceScaleFactor: 1 });
    await page.route('**/__evidence', route => route.fulfill({
      contentType: 'text/html',
      body: `<!doctype html><html lang="en"><meta charset="utf-8"><title>Issue 70454 HTTP evidence</title>
        <style>
          *{box-sizing:border-box}body{margin:0;padding:52px;background:#f1f5f9;color:#0f172a;font:17px system-ui,sans-serif}
          main{max-width:1320px;margin:auto}h1{font-size:34px;margin:12px 0}p{color:#475569}#summary{padding:16px 20px;border-radius:10px;font-weight:650;background:#e2e8f0;margin:26px 0}
          section{background:white;padding:18px 24px;margin:16px 0;border:1px solid #cbd5e1;border-radius:12px}
          h2{font-size:20px;margin:0 0 12px}.badge{float:right;font-size:15px;color:#475569}
          pre{font:15px/1.6 ui-monospace,monospace;white-space:pre-wrap;overflow-wrap:anywhere;margin:10px 0 0;color:#334155}
          .status{font-weight:700}.bad{color:#b91c1c}.good{color:#047857}footer{margin-top:22px;color:#64748b;font-size:14px}
        </style><main><div>ASP.NET Core · Minimal APIs · Issue #70454</div><h1 id="label"></h1>
        <p>Browser fetches against real endpoints. Status, content type, and body below come from HTTP responses.</p>
        <div id="summary">Fetching responses…</div><div id="responses"></div>
        <footer>Results&lt;&gt; is the control. The union should execute its active IResult, not serialize the result wrapper.</footer></main></html>`
    }));
    await page.goto(`${baseUrl}/__evidence`);
    const observations = await page.evaluate(async label => {
      document.getElementById('label').textContent = label;
      const responses = [];
      for (const path of ['/results/false', '/union/false', '/results/true', '/union/true']) {
        const response = await fetch(path);
        responses.push({ path, status: response.status, contentType: response.headers.get('content-type'), body: await response.text() });
      }
      for (const item of responses) {
        const section = document.createElement('section');
        const heading = document.createElement('h2');
        heading.textContent = `GET ${item.path}`;
        const badge = document.createElement('span');
        badge.className = 'badge';
        badge.textContent = item.path.startsWith('/results/') ? 'CONTROL · Results<>' : 'C# UNION · Outcome';
        heading.appendChild(badge);
        const status = document.createElement('div');
        const expected = item.path.endsWith('/false') ? 404 : 200;
        status.className = `status ${item.status === expected ? 'good' : 'bad'}`;
        status.textContent = `HTTP ${item.status} · ${item.contentType}`;
        const body = document.createElement('pre');
        body.textContent = item.body;
        section.append(heading, status, body);
        document.getElementById('responses').appendChild(section);
      }
      const matches = [0, 2].every(index => responses[index].status === responses[index + 1].status
        && responses[index].contentType === responses[index + 1].contentType
        && responses[index].body === responses[index + 1].body);
      const summary = document.getElementById('summary');
      summary.textContent = matches ? 'PASS — Both union responses match their Results<> controls.'
        : 'BUG REPRODUCED — Union responses do not match their Results<> controls.';
      summary.className = matches ? 'good' : 'bad';
      return { matches, responses };
    }, label);
    await page.screenshot({ path: output, fullPage: true });
    console.log(JSON.stringify(observations, null, 2));
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

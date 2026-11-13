from playwright.sync_api import sync_playwright
import time, os
url = 'http://127.0.0.1:4320'
os.makedirs('evidence', exist_ok=True)
with sync_playwright() as pw:
    b = pw.chromium.launch(channel='chrome')
    ctx = b.new_context()
    ctx.add_cookies([dict(name='transjap_access_token', value='fixture', url=url)])
    p = ctx.new_page()
    p.on('pageerror', lambda e: print('pageerror', e, flush=True))
    for w, h in [(1920, 1080), (1366, 768), (768, 1024), (390, 844)]:
        p.set_viewport_size(dict(width=w, height=h))
        p.goto(url + '/?fleetNumber=84')
        panel = p.locator('section[aria-label="Utilização calculável"]')
        try:
            panel.wait_for(timeout=6000)
        except:
            print(p.locator('body').inner_text()[:500])
            raise
        panel.scroll_into_view_if_needed()
        p.locator('summary').filter(has_text='Frota 84').first.click()
        p.get_by_text('Utilização calculável', exact=True).evaluate('el=>el.scrollIntoView()')
        p.screenshot(path=f'evidence/{w}.png')
        assert p.evaluate('document.documentElement.scrollWidth') <= w
        assert panel.locator('table a').first.get_attribute('href').startswith('/horimetros/')
        print(w, p.evaluate('document.documentElement.scrollWidth'), panel.locator('figure').count(), flush=True)
    p.get_by_role('link', name='Próxima').click()
    p.get_by_text('21 registros · Página 2', exact=False).wait_for()
    assert '5 HOURS' in panel.inner_text()
    for fleet, expected in [('900', 'Sem leituras'), ('901', 'Não foi possível apurar')]:
        p.goto(url + '/?fleetNumber=' + fleet)
        p.get_by_text(expected, exact=False).first.wait_for()
    p.goto(url + '/relatorios?fleetNumber=901')
    p.get_by_text('Reduza o período', exact=False).first.wait_for()
    p.goto(url + '/relatorios')
    p.get_by_text('Utilização calculável', exact=True).scroll_into_view_if_needed()
    p.screenshot(path='evidence/relatorios.png')
    for n in ['84', '900', '901', '902']:
        for fmt in ['xlsx', 'pdf']:
            start = time.perf_counter()
            r = ctx.request.get(url + '/api/reports/utilization/' + fmt + '?fleetNumber=' + n, timeout=120000)
            print(n, fmt, r.status, round(time.perf_counter() - start, 3), flush=True)
            assert r.status == {'900': 404, '901': 400}.get(n, 200)
            if r.ok:
                open('evidence/frota-' + n + '.' + fmt, 'wb').write(r.body())
    ctx.add_cookies([dict(name='transjap_access_token', value='operator', url=url)])
    assert ctx.request.get(url + '/api/reports/utilization/pdf').status == 403
from openpyxl import load_workbook
import fitz
for fleet, count in [('84', 21), ('902', 10000)]:
    book = load_workbook(f'evidence/frota-{fleet}.xlsx', read_only=True)
    sheet = book.active
    assert sheet.max_row == count + 5
    values = list(sheet.values)
    assert f'fleetNumber={fleet}' in values[2][0]
    assert round(sum((r[4] or 0 for r in values[4:-1])), 2) == (count - 1) * 0.25
    assert values[-2][3] == 100 + (count - 1) * 0.25
    doc = fitz.open(f'evidence/frota-{fleet}.pdf')
    text = ''.join((page.get_text() for page in doc))
    assert 'São João' in text and f'fleetNumber={fleet}' in text and (text.count(f'Frota: {fleet}') == count)
    for page in doc:
        for x0, y0, x1, y1, *_ in page.get_text('blocks'):
            assert x0 >= 0 and y0 >= 0 and (x1 <= page.rect.width) and (y1 <= page.rect.height)
    print(f'Inspected {count} rows and {len(doc)} PDF pages', flush=True)

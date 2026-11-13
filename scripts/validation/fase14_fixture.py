from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs
import json

class Handler(BaseHTTPRequestHandler):

    def do_GET(self):
        u = urlparse(self.path)
        path = u.path
        q = parse_qs(u.query)
        status = 200
        d = {'items': [], 'totalItems': 0, 'page': 1, 'pageSize': 20, 'totalPages': 0}
        if path.endswith('/auth/me'):
            roles = ['OPERATOR'] if 'operator' in self.headers.get('Authorization', '') else ['SUPERVISOR']
            d = dict(id='demo', email='demo@example.test', fullName='Supervisor de teste', name='Supervisor', roles=roles)
        elif path.endswith('/utilization'):
            n = {'900': 0, '901': 10001, '902': 10000}.get(q.get('fleetNumber', [''])[0], 21)
            page = int(q.get('page', [1])[0])
            indices = range(n) if q.get('export') == ['true'] else range((page - 1) * 20, min(n, page * 20))

            def row(i):
                return dict(machineId='demo', readingId=f'00000000-0000-4000-8000-{i:012}', model='Escavadeira', fleetNumber=int(q.get('fleetNumber', ['84'])[0]), initial=100 + max(0, i - 1) * 0.25, final=100 + i * 0.25, hours=0.25 if i else None, at=f'2026-11-12T{i // 3600:02}:{i // 60 % 60:02}:{i % 60:02}Z', reason='CALCULATED' if i else 'MISSING_INITIAL', workSite='São João' if i > 1 else None, workSiteId='s' if i > 1 else None)

            def group(label, v):
                return [dict(label=label, value=v)] if n else []
            d = dict(hours=(n - 1) * 0.25 if n else None, calculatedIntervals=max(0, n - 1), unavailableIntervals=int(bool(n)), unassignedHours=0.25 if n else 0, byFleet=group('Frota ' + q.get('fleetNumber', ['84'])[0], (n - 1) * 0.25), bySite=group('São João', (n - 2) * 0.25), byDay=group('2026-11-12', (n - 1) * 0.25), unavailableByReason={'MISSING_INITIAL': 1} if n else {}, items=[row(i) for i in indices], totalItems=n, page=page, pageSize=20)
            if n > 10000:
                status = 400
                d = {'detail': 'Mais de 10.000 leituras. Reduza o período ou selecione uma frota.'}
        elif path.endswith('/worksites'):
            d = []
        elif path.endswith('/summary'):
            d = dict(totalMachines=1, activeMachines=1, machinesWithReadings=1, approvedReadings=21, withoutRecentReading=0, readings=21, receivedToday=0, pendingReview=0, suspectReadings=0, openAnomalies=0, byWorkSite=[], byDay=[], oldestMachines=[], updatedToday=0, withoutReading=0, pendingReadings=0, alerts=0)
        b = json.dumps(d).encode()
        self.send_response(status)
        self.send_header('Content-Type', 'application/json')
        self.end_headers()
        self.wfile.write(b)
ThreadingHTTPServer(('127.0.0.1', 4319), Handler).serve_forever()

"""Exercise the local Next production server backed by the synthetic fleet fixture."""
from concurrent.futures import ThreadPoolExecutor
from io import BytesIO
from math import ceil
from statistics import median
from time import perf_counter
from urllib.request import Request, urlopen
from zipfile import ZipFile
import fitz

def download(fmt):
    req = Request(f"http://127.0.0.1:4320/api/reports/utilization/{fmt}?fleetNumber=84",
                  headers={"Cookie": "transjap_access_token=fixture"})
    start = perf_counter()
    with urlopen(req, timeout=120) as response:
        data = response.read()
    elapsed = (perf_counter() - start) * 1000
    if fmt == "xlsx":
        with ZipFile(BytesIO(data)) as book:
            assert book.testzip() is None and "xl/worksheets/sheet1.xml" in book.namelist()
    else:
        with fitz.open(stream=data, filetype="pdf") as pdf:
            assert "fleetNumber=84" in "".join(page.get_text() for page in pdf)
    return elapsed

for fmt in ("xlsx", "pdf"):
    download(fmt)
    for users in (1, 5, 10, 30):
        with ThreadPoolExecutor(users) as pool:
            samples = sorted(pool.map(download, [fmt] * (5 * users)))
        print(f"{fmt} users={users} n={len(samples)} p50={median(samples):.1f} "
              f"p95={samples[ceil(.95 * len(samples)) - 1]:.1f}ms", flush=True)

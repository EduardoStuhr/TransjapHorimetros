import Link from "next/link";
import { apiFetch, type PagedApiResponse } from "@/services/api-client";

interface AuditEntry { id: string; action: string; entity: string; entityId: string; createdAt: string; userEmail: string | null }
export default async function AuditPage({ searchParams }: { searchParams: Promise<{page?: string; entityId?: string}> }) {
  const params = await searchParams;
  const page = Math.max(1, Math.min(1000000, Number(params.page) || 1));
  const query = new URLSearchParams({ page: String(Math.floor(page)) });
  if (params.entityId) query.set("entityId", params.entityId);
  const result = await apiFetch<PagedApiResponse<AuditEntry>>("/api/v1/audit?" + query);
  const pageUrl = (p: number) => { const q = new URLSearchParams(query); q.set("page", String(p)); return "/auditoria?" + q; };
  return <main className="space-y-4"><h1 className="text-2xl font-bold">Auditoria operacional</h1>
    <form><label>ID do recurso<input className="mx-2 rounded border p-2" name="entityId" defaultValue={params.entityId} /></label><button className="rounded border p-2">Filtrar</button></form>
    <div className="overflow-auto"><table className="w-full text-left text-sm"><thead><tr>{["Data UTC","Ação","Recurso","Identificador","Responsável"].map(h => <th className="p-2" key={h}>{h}</th>)}</tr></thead>
    <tbody>{result.items.map(a => <tr key={a.id} className="border-t"><td className="p-2">{a.createdAt}</td><td>{a.action}</td><td>{a.entity}</td><td>{a.entityId}</td><td>{a.userEmail ?? "Sistema"}</td></tr>)}</tbody></table></div>
    <nav className="flex gap-4" aria-label="Páginas de auditoria">{page > 1 && <Link href={pageUrl(page-1)}>Anterior</Link>}<span>{result.totalItems} registros</span>{page < result.totalPages && <Link href={pageUrl(page+1)}>Próxima</Link>}</nav>
  </main>;
}

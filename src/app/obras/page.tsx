import type { Metadata } from "next";
import { MapPinned } from "lucide-react";

import { EmptyState } from "@/components/shared/empty-state";
import { PageHeading } from "@/components/shared/page-heading";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { WorkSiteDialog } from "@/features/worksites/components/worksite-create-dialog";
import { formatDateTime } from "@/lib/domain-formatters";
import { getWorkSites } from "@/services/worksites.service";

export const metadata: Metadata = {
  title: "Obras",
};

export default async function WorkSitesPage() {
  const workSites = await getWorkSites();

  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Operação"
        title="Obras"
        description="Gerencie os locais de operação associados às máquinas e leituras."
        action={<WorkSiteDialog />}
      />
      {workSites.length === 0 ? (
        <Card className="gap-0 border-slate-200 py-0 shadow-sm">
          <EmptyState
            icon={MapPinned}
            title="Nenhuma obra cadastrada"
            description="Cadastre a primeira obra para associá-la às próximas leituras."
          />
        </Card>
      ) : (
        <Card className="gap-0 overflow-hidden border-slate-200 py-0 shadow-sm">
          <CardContent className="p-0">
            <Table>
              <TableHeader className="bg-slate-100">
                <TableRow>
                  <TableHead>Nome</TableHead>
                  <TableHead>Código</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Atualizada em</TableHead>
                  <TableHead className="text-right">Ações</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {workSites.map((workSite) => (
                  <TableRow key={workSite.id}>
                    <TableCell className="font-semibold">
                      {workSite.name}
                    </TableCell>
                    <TableCell>{workSite.code}</TableCell>
                    <TableCell>
                      <Badge
                        variant="outline"
                        className={
                          workSite.active
                            ? "border-emerald-200 bg-emerald-50 text-emerald-800"
                            : "border-slate-200 bg-slate-50 text-slate-600"
                        }
                      >
                        {workSite.active ? "Ativa" : "Inativa"}
                      </Badge>
                    </TableCell>
                    <TableCell>{formatDateTime(workSite.updatedAt)}</TableCell>
                    <TableCell className="text-right">
                      <WorkSiteDialog workSite={workSite} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

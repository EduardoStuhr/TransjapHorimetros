"use client";

import Image from "next/image";
import { useRouter } from "next/navigation";
import { type FormEvent, useState } from "react";
import { Camera, Eye, LoaderCircle, Upload } from "lucide-react";

import { useAuth } from "@/components/auth/auth-provider";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { formatDateTime } from "@/lib/domain-formatters";
import type { PhotoEvidence } from "@/types/domain";

const uploadRoles = new Set([
  "SUPERVISOR",
  "ADMINISTRATIVE",
  "MANAGER",
  "ADMIN",
]);
const maximumFileSizeBytes = 10 * 1024 * 1024;

function formatBytes(bytes: number) {
  return new Intl.NumberFormat("pt-BR", {
    maximumFractionDigits: 1,
  }).format(bytes / 1024 / 1024) + " MB";
}

export function PhotoEvidencePanel({
  readingId,
  evidence,
}: {
  readingId: string;
  evidence: PhotoEvidence | null;
}) {
  const { user } = useAuth();
  const router = useRouter();
  const [uploading, setUploading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const canUpload = user.roles.some((role) => uploadRoles.has(role));
  const contentUrl = `/api/readings/${encodeURIComponent(readingId)}/photo/content`;

  async function handleUpload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const formData = new FormData(form);
    const file = formData.get("file");
    if (!(file instanceof File) || file.size === 0) {
      setMessage("Selecione uma imagem JPEG, PNG ou WEBP.");
      return;
    }
    if (file.size > maximumFileSizeBytes) {
      setMessage("A imagem deve possuir no máximo 10 MB.");
      return;
    }

    const capturedAt = formData.get("capturedAtDevice");
    if (typeof capturedAt === "string" && capturedAt) {
      formData.set("capturedAtDevice", new Date(capturedAt).toISOString());
    } else {
      formData.delete("capturedAtDevice");
    }
    formData.set("clientEventId", crypto.randomUUID());

    setUploading(true);
    setMessage(null);
    try {
      const response = await fetch(
        `/api/readings/${encodeURIComponent(readingId)}/photo`,
        { method: "POST", body: formData },
      );
      if (response.status === 401) {
        router.push(`/login?returnTo=${encodeURIComponent(`/horimetros/${readingId}`)}`);
        return;
      }
      if (!response.ok) {
        const problem = (await response.json().catch(() => null)) as
          | { detail?: string }
          | null;
        setMessage(problem?.detail ?? "Não foi possível enviar a evidência.");
        return;
      }

      setMessage("Evidência enviada com segurança.");
      form.reset();
      router.refresh();
    } finally {
      setUploading(false);
    }
  }

  if (!evidence) {
    return (
      <div className="rounded-lg border border-dashed border-slate-300 bg-slate-50 p-6">
        <div className="flex items-start gap-3">
          <Camera className="mt-0.5 size-5 text-slate-500" />
          <div>
            <p className="font-semibold text-slate-900">Sem evidência fotográfica</p>
            <p className="mt-1 text-sm text-slate-600">
              Nenhuma foto foi associada a esta leitura.
            </p>
          </div>
        </div>

        {canUpload && (
          <form onSubmit={handleUpload} className="mt-6 grid gap-4">
            <div className="grid gap-2">
              <Label htmlFor="photo-file">Imagem</Label>
              <Input
                id="photo-file"
                name="file"
                type="file"
                accept="image/jpeg,image/png,image/webp"
                required
              />
              <p className="text-xs text-slate-500">JPEG, PNG ou WEBP; máximo de 10 MB.</p>
            </div>
            <div className="grid gap-2 sm:max-w-xs">
              <Label htmlFor="captured-at-device">Capturada no dispositivo em</Label>
              <Input id="captured-at-device" name="capturedAtDevice" type="datetime-local" />
            </div>
            <div className="flex items-center gap-3">
              <Button type="submit" disabled={uploading}>
                {uploading ? <LoaderCircle className="animate-spin" /> : <Upload />}
                {uploading ? "Enviando..." : "Enviar evidência"}
              </Button>
              {message && <p className="text-sm text-slate-700" aria-live="polite">{message}</p>}
            </div>
          </form>
        )}
      </div>
    );
  }

  return (
    <div className="grid gap-6 md:grid-cols-[220px_minmax(0,1fr)]">
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-slate-100">
        <Image
          src={contentUrl}
          alt="Evidência fotográfica da leitura"
          width={evidence.width ?? 640}
          height={evidence.height ?? 480}
          unoptimized
          className="aspect-[4/3] h-full w-full object-cover"
        />
      </div>
      <div>
        <dl className="grid gap-4 text-sm sm:grid-cols-2">
          <div>
            <dt className="font-medium text-slate-500">Tipo</dt>
            <dd className="mt-1 font-semibold text-slate-900">{evidence.contentType}</dd>
          </div>
          <div>
            <dt className="font-medium text-slate-500">Tamanho</dt>
            <dd className="mt-1 font-semibold text-slate-900">{formatBytes(evidence.fileSizeBytes)}</dd>
          </div>
          <div>
            <dt className="font-medium text-slate-500">Enviada em</dt>
            <dd className="mt-1 font-semibold text-slate-900">{formatDateTime(evidence.uploadedAtServer)}</dd>
          </div>
          <div>
            <dt className="font-medium text-slate-500">Capturada em</dt>
            <dd className="mt-1 font-semibold text-slate-900">{formatDateTime(evidence.capturedAtDevice)}</dd>
          </div>
        </dl>
        <Dialog>
          <DialogTrigger asChild>
            <Button type="button" variant="outline" className="mt-5">
              <Eye />
              Visualizar foto
            </Button>
          </DialogTrigger>
          <DialogContent className="max-h-[92vh] sm:max-w-5xl">
            <DialogHeader>
              <DialogTitle>Evidência fotográfica</DialogTitle>
              <DialogDescription>
                Arquivo original protegido e transmitido pela API autenticada.
              </DialogDescription>
            </DialogHeader>
            <div className="flex max-h-[75vh] justify-center overflow-auto rounded-md bg-slate-950 p-2">
              <Image
                src={contentUrl}
                alt="Evidência fotográfica ampliada"
                width={evidence.width ?? 1280}
                height={evidence.height ?? 960}
                unoptimized
                className="h-auto max-h-[72vh] w-auto object-contain"
              />
            </div>
          </DialogContent>
        </Dialog>
      </div>
    </div>
  );
}

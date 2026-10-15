"use client";

import { useState, type FormEvent } from "react";
import Image from "next/image";
import { useRouter, useSearchParams } from "next/navigation";
import { Eye, EyeOff, LoaderCircle, LockKeyhole, ShieldCheck } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { PublicAuthResponse } from "@/lib/auth/types";

const DASHBOARD_ROLES = new Set(["SUPERVISOR", "MANAGER", "ADMIN"]);

export default function LoginPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    const form = new FormData(event.currentTarget);
    let response: Response;
    try {
      response = await fetch("/api/auth/login", {
        body: JSON.stringify({
          email: form.get("email"),
          password: form.get("password"),
        }),
        headers: { "Content-Type": "application/json" },
        method: "POST",
      });
    } catch {
      setError("O servico de autenticacao esta indisponivel. Tente novamente.");
      setSubmitting(false);
      return;
    }

    if (!response.ok) {
      let message = "E-mail ou senha invalidos.";
      try {
        const problem = (await response.json()) as { detail?: string };
        message = problem.detail ?? message;
      } catch {
        // Mantem uma mensagem neutra para respostas sem corpo JSON.
      }
      setError(response.status === 429 ? "Muitas tentativas. Aguarde um minuto e tente novamente." : message);
      setSubmitting(false);
      return;
    }

    const auth = (await response.json()) as PublicAuthResponse;
    const requestedPath = searchParams.get("returnTo");
    const canViewDashboard = auth.user.roles.some((role) => DASHBOARD_ROLES.has(role));
    const returnNeedsDashboardRole = requestedPath === "/"
      || requestedPath?.startsWith("/alertas")
      || requestedPath?.startsWith("/relatorios");
    const safeReturnPath = requestedPath?.startsWith("/")
      && !requestedPath.startsWith("//")
      && (!returnNeedsDashboardRole || canViewDashboard)
      ? requestedPath
      : null;
    const defaultPath = canViewDashboard
      ? "/"
      : "/frota";

    router.replace(safeReturnPath ?? defaultPath);
    router.refresh();
  }

  return (
    <main className="grid min-h-screen bg-slate-100 lg:grid-cols-[minmax(0,1.05fr)_minmax(440px,0.95fr)]">
      <section className="relative hidden overflow-hidden bg-[#10294d] p-12 text-white lg:flex lg:flex-col lg:justify-between">
        <div className="absolute inset-0 opacity-20 [background-image:linear-gradient(135deg,transparent_45%,rgba(255,255,255,.18)_45%,rgba(255,255,255,.18)_46%,transparent_46%)] [background-size:36px_36px]" />
        <div className="relative rounded-lg bg-white p-5 shadow-xl shadow-slate-950/20">
          <Image src="/logo-transjap.png" alt="Transjap" width={360} height={101} priority className="h-auto w-full max-w-sm" />
        </div>
        <div className="relative max-w-xl pb-8">
          <div className="mb-5 flex size-12 items-center justify-center rounded-lg bg-amber-400 text-slate-950">
            <ShieldCheck className="size-7" />
          </div>
          <p className="text-sm font-bold uppercase tracking-[0.2em] text-blue-200">Ambiente corporativo</p>
          <h1 className="mt-3 text-4xl font-bold leading-tight">Gestao segura de horimetros da frota.</h1>
          <p className="mt-4 max-w-lg text-lg text-blue-100/80">Acesso restrito a colaboradores autorizados da Transjap.</p>
        </div>
      </section>

      <section className="flex items-center justify-center px-5 py-10 sm:px-10">
        <div className="w-full max-w-md">
          <div className="mb-8 lg:hidden">
            <Image src="/logo-transjap.png" alt="Transjap" width={260} height={73} priority className="h-auto w-56" />
          </div>
          <p className="text-sm font-bold uppercase tracking-[0.18em] text-blue-700">Transjap Horimetros</p>
          <h2 className="mt-2 text-3xl font-bold text-slate-950">Acesse sua conta</h2>
          <p className="mt-2 text-slate-600">Use suas credenciais corporativas para continuar.</p>

          <form onSubmit={handleSubmit} className="mt-8 space-y-5">
            <div className="space-y-2">
              <Label htmlFor="email">E-mail</Label>
              <Input id="email" name="email" type="email" autoComplete="username" required autoFocus placeholder="nome@transjap.com.br" className="h-12 bg-white" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="password">Senha</Label>
              <div className="relative">
                <LockKeyhole className="pointer-events-none absolute left-3 top-3.5 size-5 text-slate-400" />
                <Input id="password" name="password" type={showPassword ? "text" : "password"} autoComplete="current-password" required className="h-12 bg-white px-10" />
                <button type="button" onClick={() => setShowPassword((value) => !value)} className="absolute right-2 top-2 flex size-8 items-center justify-center rounded text-slate-500 hover:bg-slate-100" aria-label={showPassword ? "Ocultar senha" : "Mostrar senha"}>
                  {showPassword ? <EyeOff className="size-5" /> : <Eye className="size-5" />}
                </button>
              </div>
            </div>

            {error ? <p role="alert" className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-800">{error}</p> : null}

            <Button type="submit" disabled={submitting} className="h-12 w-full text-base">
              {submitting ? <LoaderCircle className="animate-spin" /> : null}
              Entrar
            </Button>
          </form>

          <p className="mt-8 text-center text-xs text-slate-500">Sessao protegida e monitorada. Nao compartilhe suas credenciais.</p>
        </div>
      </section>
    </main>
  );
}

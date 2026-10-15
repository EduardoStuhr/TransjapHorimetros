"use client";

import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { usePathname, useRouter } from "next/navigation";

import type { AuthUser } from "@/lib/auth/types";

interface AuthContextValue {
  logout: () => Promise<void>;
  user: AuthUser;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);
  const [sessionError, setSessionError] = useState(false);

  useEffect(() => {
    const controller = new AbortController();

    async function loadUser() {
      try {
        const response = await fetch("/api/auth/me", {
          cache: "no-store",
          signal: controller.signal,
        });
        if (response.status === 401) {
          router.replace(`/login?returnTo=${encodeURIComponent(pathname)}`);
          return;
        }
        if (response.status === 403) {
          router.replace("/acesso-negado");
          return;
        }
        if (!response.ok) {
          throw new Error("Nao foi possivel validar a sessao.");
        }
        setUser((await response.json()) as AuthUser);
      } catch (error) {
        if (!controller.signal.aborted) {
          console.error("[auth] Falha ao validar a sessao:", error);
          setSessionError(true);
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      }
    }

    void loadUser();
    return () => controller.abort();
  }, [pathname, router]);

  const value = useMemo<AuthContextValue | null>(() => {
    if (!user) return null;
    return {
      user,
      logout: async () => {
        await fetch("/api/auth/logout", { method: "POST" });
        setUser(null);
        router.replace("/login");
        router.refresh();
      },
    };
  }, [router, user]);

  if (sessionError) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-100 px-6 text-center">
        <p className="font-semibold text-slate-800">Nao foi possivel validar sua sessao.</p>
        <button
          type="button"
          onClick={() => window.location.reload()}
          className="rounded-md bg-blue-900 px-4 py-2 text-sm font-semibold text-white"
        >
          Tentar novamente
        </button>
      </div>
    );
  }

  if (loading || !value) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-100 text-sm font-semibold text-slate-600">
        Validando sessao segura...
      </div>
    );
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth deve ser usado dentro de AuthProvider.");
  }
  return context;
}

"use client";

import { useEffect, useState } from "react";
import { Download, Share, X } from "lucide-react";

import { Button } from "@/components/ui/button";

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: "accepted" | "dismissed" }>;
}

const dismissalKey = "transjap:pwa-install-dismissed";

function isIosDevice() {
  return /iPad|iPhone|iPod/.test(navigator.userAgent)
    || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
}

function isStandalone() {
  return window.matchMedia("(display-mode: standalone)").matches
    || Boolean((navigator as Navigator & { standalone?: boolean }).standalone);
}

export function InstallPrompt() {
  const [installEvent, setInstallEvent] = useState<BeforeInstallPromptEvent | null>(null);
  const [showIosHelp, setShowIosHelp] = useState(false);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    if (isStandalone() || sessionStorage.getItem(dismissalKey)) return;
    const iosTimer = window.setTimeout(() => {
      if (!isIosDevice()) return;
      setShowIosHelp(true);
      setVisible(true);
    }, 0);
    const handleInstallPrompt = (event: Event) => {
      event.preventDefault();
      setInstallEvent(event as BeforeInstallPromptEvent);
      setVisible(true);
    };
    const handleInstalled = () => setVisible(false);
    window.addEventListener("beforeinstallprompt", handleInstallPrompt);
    window.addEventListener("appinstalled", handleInstalled);
    return () => {
      window.clearTimeout(iosTimer);
      window.removeEventListener("beforeinstallprompt", handleInstallPrompt);
      window.removeEventListener("appinstalled", handleInstalled);
    };
  }, []);

  function dismiss() {
    sessionStorage.setItem(dismissalKey, "1");
    setVisible(false);
  }

  async function install() {
    if (!installEvent) return;
    await installEvent.prompt();
    const choice = await installEvent.userChoice;
    if (choice.outcome === "accepted") setVisible(false);
    setInstallEvent(null);
  }

  if (!visible) return null;

  return (
    <aside className="fixed inset-x-3 bottom-[calc(5.75rem+env(safe-area-inset-bottom))] z-50 mx-auto max-w-md rounded-xl border border-blue-200 bg-white p-4 shadow-2xl lg:bottom-5" aria-label="Instalar aplicativo">
      <div className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-blue-950 text-white">{showIosHelp ? <Share className="size-5" /> : <Download className="size-5" />}</span>
        <div className="min-w-0 flex-1">
          <p className="font-bold text-slate-950">Use como aplicativo</p>
          <p className="mt-1 text-sm text-slate-600">{showIosHelp ? "No Safari, toque em Compartilhar e depois em Adicionar à Tela de Início." : "Instale a coleta Transjap para abrir rapidamente em campo."}</p>
        </div>
        <Button type="button" variant="ghost" size="icon" className="size-11" onClick={dismiss} aria-label="Fechar sugestão de instalação"><X /></Button>
      </div>
      {installEvent ? <Button type="button" className="mt-3 min-h-11 w-full" onClick={() => void install()}><Download /> Instalar aplicativo</Button> : null}
    </aside>
  );
}

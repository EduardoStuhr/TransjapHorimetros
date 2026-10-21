import {
  BellRing,
  ChartNoAxesCombined,
  FileChartColumn,
  Gauge,
  HardHat,
  MapPinned,
  ClipboardPlus,
  RefreshCw,
} from "lucide-react";

export const navigationItems = [
  { label: "Dashboard", href: "/", icon: ChartNoAxesCombined, roles: ["SUPERVISOR", "MANAGER", "ADMIN"] },
  { label: "Frota", href: "/frota", icon: HardHat, roles: null },
  { label: "Horímetros", href: "/horimetros", icon: Gauge, roles: null },
  { label: "Registrar leitura", href: "/coleta", icon: ClipboardPlus, roles: null },
  { label: "Sincronização", href: "/sincronizacao", icon: RefreshCw, roles: null },
  { label: "Obras", href: "/obras", icon: MapPinned, roles: null },
  { label: "Alertas", href: "/alertas", icon: BellRing, roles: ["SUPERVISOR", "MANAGER", "ADMIN"] },
  { label: "Relatórios", href: "/relatorios", icon: FileChartColumn, roles: ["SUPERVISOR", "MANAGER", "ADMIN"] },
] as const;

export function canAccessNavigationItem(
  roles: readonly string[],
  item: (typeof navigationItems)[number],
) {
  return item.roles === null || item.roles.some((role) => roles.includes(role));
}

const PRIVILEGED_ROLES = new Set(["SUPERVISOR", "MANAGER", "ADMIN"]);

export function isOperatorOnly(roles: readonly string[]) {
  return roles.includes("OPERATOR") && !roles.some((role) => PRIVILEGED_ROLES.has(role));
}

export function navigationItemsForRoles(roles: readonly string[]) {
  const allowed = navigationItems.filter((item) => canAccessNavigationItem(roles, item));
  return isOperatorOnly(roles)
    ? allowed.filter((item) => item.href === "/coleta" || item.href === "/sincronizacao")
    : allowed;
}

export function defaultAuthenticatedPath(roles: readonly string[]) {
  return roles.some((role) => PRIVILEGED_ROLES.has(role)) ? "/" : "/coleta";
}

export function isOperatorPathAllowed(pathname: string) {
  return pathname === "/coleta"
    || pathname.startsWith("/coleta/")
    || pathname === "/sincronizacao"
    || pathname.startsWith("/sincronizacao/");
}

export function isNavigationItemActive(pathname: string, href: string) {
  if (href === "/") {
    return pathname === "/";
  }

  return pathname.startsWith(href);
}

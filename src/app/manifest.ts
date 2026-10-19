import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Transjap Horímetros",
    short_name: "Horímetros",
    description: "Registro e sincronização confiável de leituras de máquinas.",
    start_url: "/coleta",
    display: "standalone",
    background_color: "#f1f5f9",
    theme_color: "#0f2f57",
    icons: [
      { src: "/simbolo-transjap.png", sizes: "any", type: "image/png" },
    ],
  };
}

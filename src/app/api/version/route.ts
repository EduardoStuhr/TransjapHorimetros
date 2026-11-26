export function GET() {
  return Response.json(
    { commit: process.env.BUILD_COMMIT_SHA ?? "unknown" },
    { headers: { "Cache-Control": "no-store" } },
  );
}

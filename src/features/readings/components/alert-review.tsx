"use client";
import { useActionState } from "react";
import { reviewAlert } from "@/app/alertas/actions";
export function AlertReview({ id, status }: { id: string; status: string }) {
  const [message, action, pending] = useActionState(reviewAlert, "");
  return (
    <form
      action={action}
      className="space-y-2 border-t py-3"
      onSubmit={(e) => {
        if (!window.confirm("Confirmar revisão do alerta?")) e.preventDefault();
      }}
    >
      <input type="hidden" name="id" value={id} />
      <input type="hidden" name="expectedStatus" value={status} />
      <label>
        Justificativa
        <input
          className="mx-2 rounded border p-2"
          name="reason"
          required
          minLength={3}
          maxLength={1000}
        />
      </label>
      {status === "OPEN" && (
        <button
          className="m-2 rounded border p-2"
          disabled={pending}
          name="status"
          value="ACKNOWLEDGED"
        >
          Reconhecer
        </button>
      )}
      {status !== "RESOLVED" && (
        <button
          className="rounded border p-2"
          disabled={pending}
          name="status"
          value="RESOLVED"
        >
          Resolver
        </button>
      )}
      <p role="status">{message}</p>
    </form>
  );
}

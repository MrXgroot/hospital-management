import { isAxiosError } from 'axios';
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';

export interface ApiProblem {
  title?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
  [extension: string]: unknown;
}

/** Friendlier wording for codes users commonly hit. Others fall back to the server's title. */
const MESSAGES: Record<string, string> = {
  SLOT_UNAVAILABLE: 'That slot was just taken. Please pick another one.',
  CONCURRENCY_CONFLICT: 'Someone else changed this record. Reload and try again.',
  INVALID_CREDENTIALS: 'Invalid username or password.',
  FORBIDDEN: "You don't have permission to do that.",
  INTERNAL_ERROR: 'Something went wrong on the server. Please try again.',
};

export function getProblem(error: unknown): ApiProblem | null {
  if (isAxiosError(error) && error.response?.data && typeof error.response.data === 'object') {
    return error.response.data as ApiProblem;
  }
  return null;
}

export function getErrorCode(error: unknown): string | undefined {
  return getProblem(error)?.code;
}

export function errorMessage(error: unknown): string {
  const problem = getProblem(error);
  if (problem) {
    if (problem.code && MESSAGES[problem.code]) return MESSAGES[problem.code];
    if (problem.code === 'VALIDATION_FAILED' && problem.errors) {
      const first = Object.values(problem.errors)[0]?.[0];
      if (first) return first;
    }
    if (problem.title) return problem.title;
  }
  if (isAxiosError(error)) {
    if (!error.response) return 'Cannot reach the server. Check that the API is running.';
    if (error.response.status === 403) return MESSAGES.FORBIDDEN;
  }
  return error instanceof Error ? error.message : 'Unexpected error.';
}

/** Puts server-side field errors on the matching form fields. Returns true if any were applied. */
export function applyFieldErrors<T extends FieldValues>(error: unknown, setError: UseFormSetError<T>): boolean {
  const errors = getProblem(error)?.errors;
  if (!errors) return false;
  let applied = false;
  for (const [field, messages] of Object.entries(errors)) {
    if (messages.length) {
      setError(field as Path<T>, { type: 'server', message: messages[0] });
      applied = true;
    }
  }
  return applied;
}

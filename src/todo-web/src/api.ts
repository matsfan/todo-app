import type { TodoItem } from './types'

const BASE = '/api/todos'

async function send<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: init?.body ? { 'Content-Type': 'application/json' } : undefined,
  })

  if (!response.ok) {
    throw new Error(await describeFailure(response))
  }

  // 204 No Content has no body to parse.
  return (response.status === 204 ? undefined : await response.json()) as T
}

/** Turns an ASP.NET ProblemDetails / ValidationProblemDetails body into a readable message. */
async function describeFailure(response: Response): Promise<string> {
  try {
    const problem = await response.json()
    const errors = problem?.errors as Record<string, string[]> | undefined
    if (errors) {
      const messages = Object.values(errors).flat()
      if (messages.length > 0) {
        return messages.join(' ')
      }
    }
    if (typeof problem?.title === 'string') {
      return problem.title
    }
  } catch {
    // Fall through to the status line.
  }
  return `Request failed (${response.status})`
}

export const api = {
  list: () => send<TodoItem[]>(BASE),

  create: (title: string) =>
    send<TodoItem>(BASE, { method: 'POST', body: JSON.stringify({ title }) }),

  update: (id: string, title: string, isComplete: boolean) =>
    send<TodoItem>(`${BASE}/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ title, isComplete }),
    }),

  remove: (id: string) => send<void>(`${BASE}/${id}`, { method: 'DELETE' }),
}

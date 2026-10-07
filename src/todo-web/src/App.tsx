import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { api } from './api'
import type { Filter, TodoItem } from './types'

const FILTERS: Filter[] = ['all', 'active', 'completed']

export default function App() {
  const [todos, setTodos] = useState<TodoItem[]>([])
  const [draft, setDraft] = useState('')
  const [filter, setFilter] = useState<Filter>('all')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const run = useCallback(async (action: () => Promise<void>) => {
    try {
      setError(null)
      await action()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Something went wrong')
    }
  }, [])

  useEffect(() => {
    void run(async () => {
      setTodos(await api.list())
    }).finally(() => setLoading(false))
  }, [run])

  const addTodo = (e: FormEvent) => {
    e.preventDefault()
    const title = draft.trim()
    if (!title) return

    void run(async () => {
      const created = await api.create(title)
      setTodos((current) => [created, ...current])
      setDraft('')
    })
  }

  const toggle = (todo: TodoItem) =>
    void run(async () => {
      const updated = await api.update(todo.id, todo.title, !todo.isComplete)
      setTodos((current) => current.map((t) => (t.id === updated.id ? updated : t)))
    })

  const remove = (todo: TodoItem) =>
    void run(async () => {
      await api.remove(todo.id)
      setTodos((current) => current.filter((t) => t.id !== todo.id))
    })

  const visible = useMemo(
    () =>
      todos.filter((t) =>
        filter === 'active' ? !t.isComplete : filter === 'completed' ? t.isComplete : true,
      ),
    [todos, filter],
  )

  const remaining = todos.filter((t) => !t.isComplete).length

  return (
    <main className="app">
      <header className="app__header">
        <h1>Todo</h1>
        <p className="app__count">
          {remaining === 0
            ? todos.length === 0
              ? 'Nothing here yet'
              : 'All done'
            : `${remaining} remaining`}
        </p>
      </header>

      <form className="composer" onSubmit={addTodo}>
        <input
          className="composer__input"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          placeholder="What needs doing?"
          maxLength={200}
          aria-label="New todo"
        />
        <button className="composer__submit" type="submit" disabled={!draft.trim()}>
          Add
        </button>
      </form>

      {error && (
        <p className="banner banner--error" role="alert">
          {error}
        </p>
      )}

      <nav className="filters" aria-label="Filter todos">
        {FILTERS.map((f) => (
          <button
            key={f}
            type="button"
            className="filters__tab"
            aria-current={filter === f}
            onClick={() => setFilter(f)}
          >
            {f}
          </button>
        ))}
      </nav>

      {loading ? (
        <p className="empty">Loading…</p>
      ) : visible.length === 0 ? (
        <p className="empty">
          {filter === 'all' ? 'Add your first todo above.' : `No ${filter} todos.`}
        </p>
      ) : (
        <ul className="list">
          {visible.map((todo) => (
            <li key={todo.id} className="item">
              <label className="item__label">
                <input
                  type="checkbox"
                  checked={todo.isComplete}
                  onChange={() => toggle(todo)}
                />
                <span className={todo.isComplete ? 'item__title item__title--done' : 'item__title'}>
                  {todo.title}
                </span>
              </label>
              <button
                type="button"
                className="item__delete"
                onClick={() => remove(todo)}
                aria-label={`Delete "${todo.title}"`}
              >
                ×
              </button>
            </li>
          ))}
        </ul>
      )}
    </main>
  )
}

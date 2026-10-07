export interface TodoItem {
  id: string
  title: string
  isComplete: boolean
  createdAt: string
  completedAt: string | null
}

export type Filter = 'all' | 'active' | 'completed'

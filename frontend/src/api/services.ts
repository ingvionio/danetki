import { apiClient } from './client'

export type LoginResponse = {
  user_id: string
  token: string
  expires_at: number
}

export type Puzzle = {
  puzzle_id: string
  open_part: string
  source_url: string
  created_at: number
}

export type PuzzlesResponse = {
  puzzles: Puzzle[]
  total: number
  page: number
}

export type StartParserResponse = {
  job_id: string
  message: string
}

export type ParserStatusResponse = {
  job_id: string
  status: string
  total_found: number
  total_queued: number
  total_skipped: number
  puzzles_created: number
  error: string
  started_at: number
  finished_at: number
}

export type ParserJob = {
  job_id: string
  status: string
  source_url: string
  limit: number
  total_found: number
  total_queued: number
  total_skipped: number
  puzzles_created: number
  error: string
  started_at: number
  finished_at: number
}

export type ParserJobsResponse = {
  jobs: ParserJob[]
  total: number
  page: number
}

export async function login(email: string, password: string): Promise<LoginResponse> {
  const { data } = await apiClient.post<LoginResponse>('/auth/login', { email, password })
  return data
}

export type PuzzleAnswer = {
  puzzle_id: string
  hidden_part: string
}

export async function getPuzzles(page: number, pageSize: number): Promise<PuzzlesResponse> {
  const { data } = await apiClient.get<PuzzlesResponse>('/puzzles', {
    params: { page, page_size: pageSize },
  })
  return data
}

export async function getPuzzleAnswer(puzzleId: string): Promise<PuzzleAnswer> {
  const { data } = await apiClient.get<PuzzleAnswer>(`/puzzles/${puzzleId}/answer`)
  return data
}

export async function startParser(limit: number, sourceUrl: string): Promise<StartParserResponse> {
  const { data } = await apiClient.post<StartParserResponse>('/parser/start', {
    limit,
    source_url: sourceUrl,
  })
  return data
}

export async function getParserStatus(jobId: string): Promise<ParserStatusResponse> {
  const { data } = await apiClient.get<ParserStatusResponse>('/parser/status', {
    params: { job_id: jobId },
  })
  return data
}

export async function listParserJobs(page: number, pageSize: number): Promise<ParserJobsResponse> {
  const { data } = await apiClient.get<ParserJobsResponse>('/parser/jobs', {
    params: { page, page_size: pageSize },
  })
  return data
}

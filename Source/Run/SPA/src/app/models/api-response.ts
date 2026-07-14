export interface ApiResponse<T> {
  data: T;
  errors: ApiError[];
}

export interface ApiError {
  message: string;
  code?: string;
  field?: string;
}

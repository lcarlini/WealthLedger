export interface PagedResponse<T> {
  page: number;
  pageSize: number;
  totalCount: number;
  items: T[];
}

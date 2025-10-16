export interface ViewState<T> {
  data?: T;
  isLoading: boolean;
  error?: string;
}

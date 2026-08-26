export interface AppHttpError {
  status: number;
  message: string;
  fieldErrors?: Record<string, string[]>;
}

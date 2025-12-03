export interface SmtpConfig {
  id: number | null;
  name: string;
  host: string;
  port: number;
  email: string;
  password: string;
  useSsl: boolean;
  enableBcc: boolean;
  bccEmail: string;
}
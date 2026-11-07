/** ASP.NET Core ProblemDetails as written by ExceptionHandlingMiddleware and [ApiController] validation. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  /** Present on ValidationProblemDetails (model binding / data annotation failures). */
  errors?: Record<string, string[]>;
}

declare namespace NodeJS {
  interface ProcessEnv {
    /** Injected at build time by webpack DefinePlugin. */
    BACKEND_URL: string;
  }
}

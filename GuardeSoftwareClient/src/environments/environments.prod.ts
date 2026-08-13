export const environment = {
  production: true,
  // Nginx proxies /api and /hubs to the backend. Same-origin requests avoid
  // the CORS preflight that previously preceded every authenticated call.
  apiUrl: '/api'
};

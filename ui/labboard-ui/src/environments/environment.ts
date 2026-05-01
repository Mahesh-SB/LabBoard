export const environment = {
  production: false,
  apiBaseUrl:           'http://localhost:5100',
  userManagementApiUrl: 'http://localhost:5400',
  gatewayBaseUrl:       'http://localhost:5200',
  oauth: {
    authorizeUrl: 'http://localhost:5100/oauth/authorize',
    clientId:     'e4a6c8f0b2d4e6a8c0f2b4d6e8a0c2f4',
    redirectUri:  'http://localhost:5200/oauth/callback',
    scope:        'openid profile email'
  }
};

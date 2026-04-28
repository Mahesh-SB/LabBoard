import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';

let redirecting = false;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && !redirecting) {
        redirecting = true;
        const { oauth } = environment;
        const state = encodeURIComponent(window.location.href);
        window.location.href =
          `${oauth.authorizeUrl}` +
          `?response_type=code` +
          `&client_id=${encodeURIComponent(oauth.clientId)}` +
          `&redirect_uri=${encodeURIComponent(oauth.redirectUri)}` +
          `&scope=${encodeURIComponent(oauth.scope)}` +
          `&state=${state}`;
      }
      return throwError(() => err);
    })
  );
};

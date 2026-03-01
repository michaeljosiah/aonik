import { useEffect, useState } from "react";
import { NavLink, useNavigate, useSearchParams } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";

const pkceCompletions = new Map<string, Promise<string>>();

const getOrCreatePkceCompletion = (state: string, run: () => Promise<string>) => {
  const existing = pkceCompletions.get(state);
  if (existing) {
    return existing;
  }

  const created = run().finally(() => {
    pkceCompletions.delete(state);
  });

  pkceCompletions.set(state, created);
  return created;
};

export const AuthCallback = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { completePkceLogin } = useAuth();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const error = searchParams.get("error");
    const errorDescription = searchParams.get("error_description");
    const authorizationCode = searchParams.get("code");
    const state = searchParams.get("state");

    if (error) {
      setErrorMessage(errorDescription || "Sign in was cancelled.");
      return () => {
        cancelled = true;
      };
    }

    if (!authorizationCode || !state) {
      setErrorMessage("Sign-in response is missing required parameters.");
      return () => {
        cancelled = true;
      };
    }

    const completeLogin = async () => {
      try {
        const returnTo = await getOrCreatePkceCompletion(
          state,
          () => completePkceLogin(authorizationCode, state)
        );
        if (cancelled) {
          return;
        }

        navigate(returnTo, { replace: true });
      } catch (exception) {
        if (cancelled) {
          return;
        }

        if (exception instanceof Error && exception.message.trim()) {
          setErrorMessage(exception.message);
          return;
        }

        setErrorMessage("Unable to complete sign in. Please try again.");
      }
    };

    void completeLogin();

    return () => {
      cancelled = true;
    };
  }, [completePkceLogin, navigate, searchParams]);

  return (
    <div className="fullscreen-xl">
      <div className="container py-5">
        <div className="row justify-content-center">
          <div className="col-lg-6">
            <div className="login-content text-center">
              <img className="mb-4" src="/images/payabo_logo_horizontal.png" alt="Payabo" />
              {!errorMessage && (
                <>
                  <h4>Signing you in...</h4>
                  <p className="text-muted mb-0">Please wait while we complete your secure login.</p>
                </>
              )}
              {errorMessage && (
                <>
                  <h4>Sign in failed</h4>
                  <p className="text-danger">{errorMessage}</p>
                  <p>
                    <NavLink to="/login">Return to login</NavLink>
                  </p>
                </>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

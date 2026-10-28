import { createContext, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { api, getToken, setToken } from './api';
import type { AuthResponse, ProfileDto } from './api';

interface AuthCtx {
  token: string | null;
  profile: ProfileDto | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<ProfileDto>;
  logout: () => void;
}

const Ctx = createContext<AuthCtx>({
  token: null,
  profile: null,
  loading: true,
  login: async () => undefined as unknown as ProfileDto,
  logout: () => {}
});

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setTokenState] = useState<string | null>(getToken());
  const [profile, setProfile] = useState<ProfileDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (!getToken()) {
        setLoading(false);
        return;
      }
      try {
        const me = await api.get<ProfileDto>('/api/auth/me');
        if (!cancelled) setProfile(me);
      } catch {
        setToken(null);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const login = async (email: string, password: string) => {
    const res = await api.post<AuthResponse>('/api/auth/login', { email, password });
    setToken(res.token);
    setTokenState(res.token);
    setProfile(res.profile);
    return res.profile;
  };

  const logout = () => {
    setToken(null);
    setTokenState(null);
    setProfile(null);
  };

  return <Ctx.Provider value={{ token, profile, loading, login, logout }}>{children}</Ctx.Provider>;
}

export const useAuth = () => useContext(Ctx);

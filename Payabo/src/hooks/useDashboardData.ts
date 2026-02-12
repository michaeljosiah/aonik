import { useCallback, useEffect, useRef, useState } from "react";

import {
  getDashboardSummary,
  type DashboardRecentTransaction,
  type DashboardUpcomingBill
} from "../api/dashboard";

type UseDashboardDataResult = {
  upcomingBills: DashboardUpcomingBill[];
  recentTransactions: DashboardRecentTransaction[];
  isLoading: boolean;
  errorMessage: string | null;
  refresh: () => Promise<void>;
};

export const useDashboardData = (userId: string | null | undefined): UseDashboardDataResult => {
  const [upcomingBills, setUpcomingBills] = useState<DashboardUpcomingBill[]>([]);
  const [recentTransactions, setRecentTransactions] = useState<DashboardRecentTransaction[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const requestSequence = useRef(0);

  const load = useCallback(async () => {
    requestSequence.current += 1;
    const currentRequest = requestSequence.current;

    if (!userId) {
      setUpcomingBills([]);
      setRecentTransactions([]);
      setErrorMessage(null);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setErrorMessage(null);

    try {
      const summary = await getDashboardSummary(userId);
      if (currentRequest !== requestSequence.current) {
        return;
      }

      setUpcomingBills(summary.upcomingBills);
      setRecentTransactions(summary.recentTransactions);
    } catch {
      if (currentRequest !== requestSequence.current) {
        return;
      }

      setUpcomingBills([]);
      setRecentTransactions([]);
      setErrorMessage("Unable to load your dashboard activity right now.");
    } finally {
      if (currentRequest === requestSequence.current) {
        setIsLoading(false);
      }
    }
  }, [userId]);

  useEffect(() => {
    void load();
  }, [load]);

  return {
    upcomingBills,
    recentTransactions,
    isLoading,
    errorMessage,
    refresh: load
  };
};

import { useQuery } from "@tanstack/react-query";
import { searchHadiths, advancedSearchHadiths } from "@/lib/api";
import { useState } from "react";
import { SearchRequestDto } from "@/types/api";

/** The search runs only for text of at least this many characters. */
export const MIN_QUERY_LENGTH = 3;

export function useHadithSearch(
  initialQuery: string = "",
  initialScope: number = 0,
  initialMatch: number = 0
) {
  // `query` is what is typed in the box. `submittedQuery` is what is searched: it changes only when the user
  // presses the search button or Enter (or picks a suggestion), not on every keystroke.
  const [query, setQuery] = useState(initialQuery);
  const [submittedQuery, setSubmittedQuery] = useState(initialQuery);
  const [scope, setScope] = useState(initialScope);
  const [match, setMatch] = useState(initialMatch);
  const [advancedRequest, setAdvancedRequest] = useState<SearchRequestDto | null>(null);

  const [page, setPage] = useState(1);
  const pageSize = 50;

  const [prevInitialQuery, setPrevInitialQuery] = useState(initialQuery);

  // Sync state if initialQuery changes from outside without causing cascading renders in useEffect
  if (initialQuery !== prevInitialQuery) {
    setPrevInitialQuery(initialQuery);
    setQuery(initialQuery);
    setSubmittedQuery(initialQuery);
    setPage(1);
  }

  /** Searches `text` (or the typed text). Ignores text shorter than MIN_QUERY_LENGTH. */
  const submitSearch = (text?: string) => {
    const value = (text ?? query).trim();
    if (value.length < MIN_QUERY_LENGTH) return;
    setQuery(value);
    setSubmittedQuery(value);
    setPage(1);
  };

  /** Clears the box and the results. */
  const clearSearch = () => {
    setQuery("");
    setSubmittedQuery("");
    setPage(1);
  };

  // Standard Query
  const standardQuery = useQuery({
    queryKey: ["search", submittedQuery, scope, match, page, pageSize],
    queryFn: () => searchHadiths(submittedQuery, scope, match, page, pageSize),
    enabled: !advancedRequest && submittedQuery.trim().length >= MIN_QUERY_LENGTH,
    staleTime: 1000 * 60 * 5,
  });

  // Advanced / Shamela Query
  const advancedQuery = useQuery({
    queryKey: ["advanced-search", advancedRequest, page, pageSize],
    queryFn: () => advancedSearchHadiths({ ...advancedRequest!, page, pageSize }),
    enabled:
      !!advancedRequest &&
      ((advancedRequest.phrases?.length ?? 0) > 0 ||
        (advancedRequest.andPhrases?.length ?? 0) > 0 ||
        (advancedRequest.orPhrases?.length ?? 0) > 0),
    staleTime: 1000 * 60 * 5,
  });

  const activeQueryResult = advancedRequest ? advancedQuery : standardQuery;

  return {
    query,
    setQuery,
    scope,
    setScope,
    match,
    setMatch,
    page,
    setPage,
    pageSize,
    submittedQuery,
    submitSearch,
    clearSearch,
    advancedRequest,
    setAdvancedRequest: (req: SearchRequestDto | null) => {
      setPage(1);
      setAdvancedRequest(req);
    },
    ...activeQueryResult,
  };
}

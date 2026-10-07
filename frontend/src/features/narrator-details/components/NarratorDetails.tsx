"use client";

import { useQuery } from "@tanstack/react-query";
import { API_BASE, getNarratorDetails } from "@/lib/api";
import { useNarratorDrawerStore } from "../store/useNarratorDrawerStore";
import { Sparkles, AlertCircle, AlertTriangle, MapPin } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import AiNotice from "@/components/AiNotice";
import GlossaryTerm from "@/features/glossary/GlossaryTerm";
import ConsensusBanner from "./ConsensusBanner";
import EvidenceBadge from "@/components/EvidenceBadge";
import {
  getGradeStyle,
  getVerdictAr,
  UNRATED_HINT,
  UNRATED_STYLE,
} from "../utils/gradeStyle";
import {
  analyzeEvaluations,
  narratorEvidence,
  type EvaluationGroup,
} from "../utils/evaluationConsensus";

const SCHOLAR_AR: Record<string, string> = {
  jarh: "ابن أبي حاتم (الجرح والتعديل)",
  thiqat: "ابن حبان (الثقات)",
  mughni_ducafa: "الذهبي (المغني في الضعفاء)",
  diwan_ducafa: "الذهبي (ديوان الضعفاء)",
  kashif: "الذهبي (الكاشف)",
  tahdhib_tahdhib: "ابن حجر (تهذيب التهذيب)",
  mizan: "الذهبي (ميزان الاعتدال)",
  tahdhib_kamal: "المزي (تهذيب الكمال)",
  taqrib: "ابن حجر (تقريب التهذيب)",
  kamil: "ابن عدي (الكامل في الضعفاء)",
  tabaqat: "ابن سعد (الطبقات الكبرى)",
  siyar: "الذهبي (سير أعلام النبلاء)",
  tarikh: "البخاري (التاريخ الكبير)",
  tarikh_islam: "الذهبي (تاريخ الإسلام)",
  durar_kamina: "ابن حجر (الدرر الكامنة)",
  isaba: "ابن حجر (الإصابة)",
  lisan_mizan: "ابن حجر (لسان الميزان)",
  tadhkirat_huffaz: "الذهبي (تذكرة الحفاظ)",
};

const getScholarAr = (en: string) => SCHOLAR_AR[en.toLowerCase()] || en;

const CAMP_HEADING: Record<EvaluationGroup["camp"], string> = {
  tadil: "من عدّله ووثّقه",
  jarh: "من جرّحه أو ضعّفه أو جهّله",
  unclassified: "أقوال غير مصنَّفة",
};

function EvaluationCard({
  evalRecord,
}: {
  evalRecord: {
    scholarName: string;
    verdictRating: string | null;
    evaluationText: string;
  };
}) {
  return (
    <div className="bg-surface-muted p-3 rounded border border-slate-100">
      <div className="flex justify-between items-start mb-1">
        <span className="font-semibold text-brand-teal-ink text-sm">
          {getScholarAr(evalRecord.scholarName)}
        </span>
        {evalRecord.verdictRating && (
          <span className="text-xs px-1.5 py-0.5 bg-surface border border-line rounded text-ink-muted">
            {getVerdictAr(evalRecord.verdictRating)}
          </span>
        )}
      </div>
      <p className="text-slate-700 text-sm italic">
        &quot;{evalRecord.evaluationText}&quot;
      </p>
    </div>
  );
}

interface ExtractedAiEvaluation {
  verbatimQuote: string;
  sourceBook: string;
  tier: string;
  justification: string;
  status: "ok" | "no-evaluations" | "unverified" | "unavailable";
}

/**
 * Everything known about one narrator: the node's own hints, grade and biography, narration counts and the critics'
 * words. Shown inside a drawer (phones, tablets) or in the workspace side panel (wide screens). Give it a `key` of
 * the narrator id so the AI summary state starts fresh for each narrator.
 */
export default function NarratorDetails() {
  const selectedNarratorId = useNarratorDrawerStore(
    (s) => s.selectedNarratorId,
  );
  const nodeContext = useNarratorDrawerStore((s) => s.nodeContext);
  const [isAiLoading, setIsAiLoading] = useState(false);
  const [aiSummary, setAiSummary] = useState<ExtractedAiEvaluation | null>(
    null,
  );
  const [aiError, setAiError] = useState<string | null>(null);

  const {
    data: narrator,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ["narrator", selectedNarratorId],
    queryFn: () => getNarratorDetails(selectedNarratorId!),
    enabled: !!selectedNarratorId,
  });

  const handleGenerateSummary = async () => {
    if (!selectedNarratorId) return;
    setIsAiLoading(true);
    setAiError(null);
    try {
      const res = await fetch(
        `${API_BASE}/Narrators/${selectedNarratorId}/ai-summary`,
      );
      if (!res.ok)
        throw new Error(
          "فشل استخراج البيانات. حاول لاحقاً أو راجع أقوال العلماء الأصلية.",
        );
      const data = await res.json();
      setAiSummary(data);
    } catch (err) {
      if (err instanceof Error) {
        setAiError(err.message);
      } else {
        setAiError("حدث خطأ غير متوقع.");
      }
    } finally {
      setIsAiLoading(false);
    }
  };

  const consensus = narrator ? analyzeEvaluations(narrator.evaluations) : null;
  const evidence =
    narrator && consensus
      ? narratorEvidence(consensus, narrator.evaluations.length)
      : null;

  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-6">
      {(nodeContext?.anomalyReason || nodeContext?.travelNote) && (
        <div className="space-y-2">
          {nodeContext.anomalyReason && (
            <p className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
              <span>
                <span className="font-bold">
                  تنبيه على هذا الموضع من السند:{" "}
                </span>
                {nodeContext.anomalyReason}
              </span>
            </p>
          )}
          {nodeContext.travelNote && (
            <p className="flex items-start gap-2 rounded-lg border border-sky-200 bg-sky-50 p-3 text-sm text-sky-900">
              <MapPin className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
              <span>{nodeContext.travelNote}</span>
            </p>
          )}
        </div>
      )}

      {isLoading ? (
        <div className="animate-pulse space-y-4">
          <div className="h-6 bg-slate-200 rounded w-3/4"></div>
          <div className="h-4 bg-slate-200 rounded w-1/2"></div>
          <div className="h-20 bg-slate-200 rounded"></div>
        </div>
      ) : isError || !narrator ? (
        <p
          role="alert"
          className="flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 p-3 text-sm font-semibold text-rose-800"
        >
          <AlertCircle className="h-4 w-4 shrink-0" aria-hidden />
          تعذر تحميل بيانات الراوي. أغلق اللوحة وأعد المحاولة.
        </p>
      ) : (
        <>
          {/* Header Info */}
          <div>
            <h3 className="text-l font-bold text-brand-dark mb-1">
              {narrator.knownAs || narrator.fullName}
            </h3>
            {narrator.knownAs && (
              <p className="text-sm text-ink-subtle mb-2">
                {narrator.fullName}
              </p>
            )}

            <div className="flex gap-2 flex-wrap mt-3">
              {narrator.generationTier && (
                <span className="px-2 py-1 bg-brand-blue/10 text-brand-blue rounded text-xs font-semibold">
                  {narrator.generationTier}
                </span>
              )}
              {narrator.gradeEn && getGradeStyle(narrator.gradeEn) ? (
                <span
                  className={`px-2 py-1 rounded border text-xs font-semibold ${getGradeStyle(narrator.gradeEn)!.badgeClass}`}
                >
                  {getVerdictAr(narrator.gradeEn)}
                </span>
              ) : (
                <span
                  className={`px-2 py-1 rounded border text-xs font-semibold ${UNRATED_STYLE.badgeClass}`}
                  title={UNRATED_HINT}
                >
                  {UNRATED_STYLE.label}
                </span>
              )}
              {narrator.gawamiRank && (
                <span className="px-2 py-1 bg-indigo-50 text-indigo-700 border border-indigo-200 rounded text-xs font-semibold">
                  الرتبة: {narrator.gawamiRank}
                </span>
              )}
              {narrator.birthYearHijri && (
                <span className="px-2 py-1 bg-slate-100 text-ink-muted rounded text-xs">
                  مواليد: {narrator.birthYearHijri} هـ
                </span>
              )}
              {narrator.deathYearHijri && (
                <span className="px-2 py-1 bg-slate-100 text-ink-muted rounded text-xs">
                  وفيات: {narrator.deathYearHijri} هـ
                </span>
              )}
              {narrator.isMudallis && (
                <span className="px-2 py-1 bg-orange-100 text-orange-800 border border-orange-300 rounded text-xs font-bold">
                  موصوف بـ<GlossaryTerm id="tadlis">التدليس</GlossaryTerm>
                </span>
              )}
              {narrator.hasMukhtalit && (
                <span className="px-2 py-1 bg-yellow-100 text-yellow-800 border border-yellow-300 rounded text-xs font-bold">
                  <GlossaryTerm id="ikhtilat">اختلط</GlossaryTerm> بأخرة
                </span>
              )}
            </div>
          </div>

          {/* Geography & Schools of Hadith */}
          {(narrator.residencePlaces || narrator.deathPlace) && (
            <div className="bg-surface-muted rounded-xl p-3.5 border border-line space-y-2">
              <h4 className="text-sm font-bold text-ink">
                🌍 البلدان والرحلة العلمية
              </h4>
              {narrator.residencePlaces && (
                <div>
                  <span className="text-xs text-ink-subtle block mb-1">
                    بلدان الإقامة والرحلة:
                  </span>
                  <div className="flex flex-wrap gap-1.5">
                    {narrator.residencePlaces
                      .split(/[،,-]/)
                      .map((city, idx) => {
                        const trimmed = city.trim();
                        if (!trimmed) return null;
                        return (
                          <span
                            key={idx}
                            className="px-2 py-0.5 bg-surface border border-line rounded-full text-xs text-slate-700 font-medium shadow-2xs"
                          >
                            📍 {trimmed}
                          </span>
                        );
                      })}
                  </div>
                </div>
              )}
              {narrator.deathPlace && (
                <div className="text-xs text-ink-muted pt-1 border-t border-line/70">
                  <span className="text-ink-subtle">بلد الوفاة: </span>
                  <span className="font-semibold text-ink">
                    {narrator.deathPlace}
                  </span>
                </div>
              )}
            </div>
          )}

          {/* Narration Volume & Tafarrud Indicator */}
          {(narrator.uniqueHadithCount != null ||
            narrator.totalNarrationsCount != null) && (
            <div className="bg-blue-50/60 rounded-xl p-3.5 border border-blue-100 space-y-2">
              <div className="flex items-center justify-between">
                <h4 className="text-sm font-bold text-ink">
                  📊 إحصائيات المرويات (جوامع الكلم)
                </h4>
                {narrator.uniqueHadithCount != null && (
                  <span
                    className={`px-2 py-0.5 text-xs font-bold rounded-full ${
                      narrator.uniqueHadithCount <= 5
                        ? "bg-amber-100 text-amber-800 border border-amber-300"
                        : narrator.uniqueHadithCount >= 500
                          ? "bg-emerald-100 text-emerald-800 border border-emerald-300"
                          : "bg-blue-100 text-blue-800 border border-blue-200"
                    }`}
                  >
                    {narrator.uniqueHadithCount <= 5
                      ? "راوٍ مُقِلّ (يُحذر من تفرده)"
                      : narrator.uniqueHadithCount >= 500
                        ? "إمام حافظ مُكثِر"
                        : "متوسط الرواية"}
                  </span>
                )}
              </div>
              <div className="grid grid-cols-2 gap-2 pt-1">
                {narrator.uniqueHadithCount != null && (
                  <div className="bg-surface p-2.5 rounded-lg border border-blue-100 text-center">
                    <div className="text-lg font-bold text-brand-dark">
                      {narrator.uniqueHadithCount.toLocaleString("ar-EG")}
                    </div>
                    <div className="text-[11px] text-ink-subtle">
                      أطراف الأحاديث الفريدة
                    </div>
                  </div>
                )}
                {narrator.totalNarrationsCount != null && (
                  <div className="bg-surface p-2.5 rounded-lg border border-blue-100 text-center">
                    <div className="text-lg font-bold text-brand-blue">
                      {narrator.totalNarrationsCount.toLocaleString("ar-EG")}
                    </div>
                    <div className="text-[11px] text-ink-subtle">
                      إجمالي الأسانيد والطرق
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Bio */}
          {narrator.biography && (
            <div>
              <h4 className="text-lg font-semibold text-ink mb-2">
                ترجمة الراوي
              </h4>
              <p className="text-ink-muted text-sm leading-relaxed whitespace-pre-wrap">
                {narrator.biography}
              </p>
            </div>
          )}

          {evidence && (
            <div className="flex flex-wrap items-center gap-2 rounded-lg border border-line bg-surface p-2.5">
              <span className="text-xs font-semibold text-ink-muted">
                حالة الدليل لحال الراوي:
              </span>
              <EvidenceBadge status={evidence.status} />
              <span className="text-xs text-ink-muted">{evidence.reason}</span>
            </div>
          )}

          {/* AI Summary Section */}
          {narrator.evaluations.length > 0 && (
            <div className="bg-purple-50 rounded-xl p-4 border border-purple-100">
              <div className="flex items-center gap-2 mb-3">
                <Sparkles className="w-5 h-5 text-purple-600" />
                <h4 className="text-purple-900 font-bold">
                  الاستخراج الذكي (AI)
                </h4>
              </div>

              {!aiSummary && !isAiLoading && !aiError && (
                <button
                  onClick={handleGenerateSummary}
                  className="w-full py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg text-sm font-semibold transition-colors"
                >
                  استخراج التقييم الأكاديمي
                </button>
              )}

              {isAiLoading && (
                <div className="text-sm text-purple-600 animate-pulse text-center py-2">
                  جاري البحث واستخراج الأقوال...
                </div>
              )}

              {aiError && (
                <div className="text-sm text-red-500 bg-red-50 p-3 rounded-lg flex gap-2 items-start">
                  <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
                  <span>{aiError}</span>
                </div>
              )}

              {aiSummary && (
                <div className="text-sm text-purple-900 leading-relaxed font-arabic space-y-2">
                  {aiSummary.status === "ok" &&
                    consensus?.status === "dispute" && (
                      <p
                        role="note"
                        className="rounded border border-amber-300 bg-amber-50 p-2 text-xs font-semibold text-amber-900"
                      >
                        العلماء مختلفون في هذا الراوي: الاقتباس أدناه قول واحد
                        من أقوالهم وليس إجماعاً. راجع بقية الأقوال.
                      </p>
                    )}
                  {aiSummary.status === "ok" ? (
                    <div className="bg-surface p-3 rounded shadow-sm border border-purple-100">
                      <p className="font-bold text-lg mb-1">
                        «{aiSummary.verbatimQuote}»
                      </p>
                      <p className="text-xs text-purple-600 mb-3">
                        — {aiSummary.sourceBook}
                      </p>
                      <div className="bg-purple-50 p-2 rounded">
                        <span className="text-xs text-slate-700">
                          {aiSummary.justification}
                        </span>
                      </div>
                    </div>
                  ) : (
                    <div className="bg-amber-50 p-3 rounded border border-amber-200 text-amber-900">
                      <p className="font-bold mb-1">غير مصنَّف آلياً</p>
                      <p className="text-xs">{aiSummary.justification}</p>
                    </div>
                  )}
                  <AiNotice />
                </div>
              )}
            </div>
          )}

          {/* Scholar Evaluations */}
          <div>
            <div className="mb-3 flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
              <h4 className="text-lg font-semibold text-ink">
                أقوال الجرح والتعديل
              </h4>
              <Link
                href="/sources#jarh-tadil"
                target="_blank"
                className="text-xs font-semibold text-brand-blue underline"
              >
                مصادر الأقوال
              </Link>
            </div>
            {narrator.evaluations.length > 0 && consensus ? (
              <>
                <ConsensusBanner consensus={consensus} />
                {consensus.status === "dispute" ? (
                  <div className="space-y-4">
                    {consensus.groups.map((group) => (
                      <section
                        key={group.camp}
                        aria-label={CAMP_HEADING[group.camp]}
                      >
                        <h5 className="mb-2 text-sm font-bold text-slate-700">
                          {CAMP_HEADING[group.camp]}{" "}
                          <span className="font-normal text-ink-subtle">
                            ({group.items.length.toLocaleString("ar-EG")})
                          </span>
                        </h5>
                        <div className="space-y-3">
                          {group.items.map(({ evaluation, index }) => (
                            <EvaluationCard
                              key={index}
                              evalRecord={evaluation}
                            />
                          ))}
                        </div>
                      </section>
                    ))}
                  </div>
                ) : (
                  <div className="space-y-3">
                    {narrator.evaluations.map((evalRecord, idx) => (
                      <EvaluationCard key={idx} evalRecord={evalRecord} />
                    ))}
                  </div>
                )}
              </>
            ) : (
              <p className="text-sm text-ink-subtle">
                لا توجد أقوال مسجلة لهذا الراوي.
              </p>
            )}
          </div>
        </>
      )}
    </div>
  );
}

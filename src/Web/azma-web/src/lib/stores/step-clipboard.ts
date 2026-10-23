import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface ClipboardStep {
  action: string;
  expectedResult: string;
}

interface StepClipboardState {
  steps: ClipboardStep[];
  sourceLabel: string | null;
  copiedAt: number | null;
  copy: (steps: ClipboardStep[], sourceLabel?: string) => void;
  clear: () => void;
}

const MAX_AGE_MS = 24 * 60 * 60 * 1000;

export const useStepClipboard = create<StepClipboardState>()(
  persist(
    (set) => ({
      steps: [],
      sourceLabel: null,
      copiedAt: null,
      copy: (steps, sourceLabel) =>
        set({ steps, sourceLabel: sourceLabel ?? null, copiedAt: Date.now() }),
      clear: () => set({ steps: [], sourceLabel: null, copiedAt: null }),
    }),
    {
      name: 'azma.step-clipboard',
      version: 1,
      onRehydrateStorage: () => (state) => {
        if (state?.copiedAt && Date.now() - state.copiedAt > MAX_AGE_MS) {
          state.clear();
        }
      },
    },
  ),
);

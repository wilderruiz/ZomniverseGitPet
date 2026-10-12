<?php

declare(strict_types=1);

/**
 * Screenshot-only fixture for demonstrating ZGit Pet reconciliation.
 * This file is not used by the application at runtime.
 */
final class ReconcileShowcase
{
    public static function title(): string
    {
        return 'ZGit Pet Reconcile Showcase';
    }

    public static function localStory(): array
    {
        return [
            'side' => 'LOCAL',
            'message' => 'Local developer refined the review workflow',
            'review' => 'ready',
        ];
    }

    public static function safetyRules(): array
    {
        return [
            'review_first' => true,
            'force_push' => false,
            'final_save_gate' => true,
        ];
    }

    public static function remoteStory(): array
    {
        return [
            'side' => 'REMOTE',
            'message' => 'Baseline remote story',
            'review' => 'waiting',
        ];
    }

    public static function footer(): string
    {
        return 'Independent changes should merge cleanly.';
    }
}

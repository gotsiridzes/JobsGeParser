export function AttributionFooter() {
  return (
    <footer className="border-t border-border/60 py-6 mt-auto">
      <div className="mx-auto max-w-3xl px-4 text-center text-xs text-muted-foreground space-y-1">
        <p>
          Job data from{' '}
          <a
            href="https://jobs.ge"
            target="_blank"
            rel="noopener noreferrer"
            className="underline underline-offset-2 hover:text-foreground"
          >
            jobs.ge
          </a>
          . Always open the original posting before you apply.
        </p>
      </div>
    </footer>
  )
}

interface PreviewProps {
  channelTitle: string
  title: string
  body: string
  linkUrl: string
  buttonText: string
  photoUrl: string | null
}

/**
 * Mesajin Telegram kanalinda nasil gorunecegini taklit eder. Sunucudaki bicimlendirmeyle ayni sirayi izler:
 * gorsel, kalin baslik, metin, "Detaylar" linki ve altta buton.
 */
export function TelegramPreview({ channelTitle, title, body, linkUrl, buttonText, photoUrl }: PreviewProps) {
  const empty = !title.trim() && !body.trim() && !photoUrl

  return (
    <div className="rounded-2xl bg-[#8fb1c9] bg-[radial-gradient(circle_at_20%_20%,#a9c6d8_0,transparent_40%),radial-gradient(circle_at_80%_60%,#9dbfb0_0,transparent_45%)] p-4">
      <div className="mx-auto max-w-sm">
        <div className="overflow-hidden rounded-2xl rounded-bl-md bg-white shadow-sm">
          {photoUrl && <img src={photoUrl} alt="" className="max-h-72 w-full object-cover" />}

          <div className="px-3 pb-1.5 pt-2 text-[14px] leading-snug text-slate-900">
            <p className="mb-1 text-[13px] font-semibold text-sky-600">{channelTitle}</p>

            {empty ? (
              <p className="text-slate-400">Mesaj önizlemesi burada görünecek.</p>
            ) : (
              <>
                {title.trim() && <p className="font-bold">{title}</p>}
                {title.trim() && body.trim() && <div className="h-3" />}
                {body.trim() && <p className="whitespace-pre-wrap break-words">{body}</p>}
                {linkUrl.trim() && (
                  <p className="mt-3">
                    🔗 <span className="text-sky-600 underline">Detaylar</span>
                  </p>
                )}
              </>
            )}

            <p className="mt-1 text-right text-[11px] text-slate-400">👁 · şimdi</p>
          </div>
        </div>

        {buttonText.trim() && (
          <div className="mt-1 rounded-lg bg-black/25 py-2 text-center text-[13px] font-medium text-white backdrop-blur-sm">
            {buttonText} ↗
          </div>
        )}
      </div>
    </div>
  )
}

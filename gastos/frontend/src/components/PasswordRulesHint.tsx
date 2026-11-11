/** What a new password has to have. The same rules when signing up and when resetting it: one text, so they cannot drift apart. */
export default function PasswordRulesHint() {
  return (
    <small className="mt-1 text-xs font-normal tracking-normal normal-case">
      Mínimo 8 caracteres, con mayúscula, minúscula, número y un carácter especial.
    </small>
  )
}

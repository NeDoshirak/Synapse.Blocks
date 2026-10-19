let pendingCodes: string[] | null = null;

export function setPendingRecoveryCodes(codes: string[]) {
  pendingCodes = [...codes];
}

export function takePendingRecoveryCodes() {
  const codes = pendingCodes;
  pendingCodes = null;
  return codes;
}

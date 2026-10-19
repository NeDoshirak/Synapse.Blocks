export function scrollOptionIntoView(root, optionId) {
    root.querySelector(`#${CSS.escape(optionId)}`)?.scrollIntoView({ block: "nearest" });
}

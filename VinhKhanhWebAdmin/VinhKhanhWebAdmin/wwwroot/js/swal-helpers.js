const vkSwal = Swal.mixin({
    customClass: {
        popup: 'vk-swal-popup',
        title: 'vk-swal-title',
        htmlContainer: 'vk-swal-text',
        confirmButton: 'vk-swal-confirm',
        cancelButton: 'vk-swal-cancel'
    },
    buttonsStyling: false
});

window.confirmDeletePoi = async function (poiName) {
    const result = await vkSwal.fire({
        title: 'Xác nh?n xóa?',
        text: `B?n có ch?c mu?n xóa ??a ?i?m "${poiName}" không?`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Xóa ngay',
        cancelButtonText: 'H?y',
        reverseButtons: true,
        focusCancel: true
    });

    return result.isConfirmed;
};

window.showSuccessMessage = async function (title, text) {
    await vkSwal.fire({
        title,
        text,
        icon: 'success',
        confirmButtonText: '?ã hi?u'
    });
};

window.showErrorMessage = async function (title, text) {
    await vkSwal.fire({
        title,
        text,
        icon: 'error',
        confirmButtonText: '?óng'
    });
};
